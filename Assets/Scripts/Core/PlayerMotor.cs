using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>
    /// 玩家移动模拟器（纯函数，确定性）。
    /// 数据流：MotorInput → Simulate → MotorState（player_movement.md 数据流节）。
    /// 禁止感知 UnityEngine / CharacterController / Transform（asmdef 无回边不变量）。
    /// </summary>
    public static class PlayerMotor
    {
        /// <summary>俯仰 clamp（±89°，弧度），防万向锁。</summary>
        private const float PitchLimitRad = 89f * System.MathF.PI / 180f;

        /// <summary>
        /// 推进一帧模拟。dt 必须为正且有限（帧率无关，计时器以秒累计）。
        /// </summary>
        public static MotorState Simulate(in MotorState current, in MotorInput input, in MotorConfig config, float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
            {
                throw new System.ArgumentOutOfRangeException("dt", "dt must be a positive finite number of seconds");
            }

            MotorState s = current;

            // --- 1. 计时器累计（帧率无关） ---
            s.TimeSinceGrounded += dt;
            s.TimeSinceJumpPressed += dt;

            // --- 2. 接地判定（上升段忽略外部 grounded 回报：CharacterController 离地方为 false，此处防御单帧毛刺） ---
            bool grounded = input.IsGrounded && s.Phase != GroundedPhase.Ascending;
            if (grounded)
            {
                s.TimeSinceGrounded = 0f;
            }

            // --- 3. 跳跃输入缓冲 ---
            if (input.JumpPressed)
            {
                s.TimeSinceJumpPressed = 0f;
            }

            // --- 4. 起跳判定：当前接地 或 coyote 窗口内，且 buffer 窗口内 ---
            bool coyoteOk = grounded || s.TimeSinceGrounded <= config.CoyoteTime;
            bool bufferOk = s.TimeSinceJumpPressed <= config.InputBuffer;
            if (coyoteOk && bufferOk && s.Phase != GroundedPhase.Ascending)
            {
                s.Velocity = new Vector3(s.Velocity.X, config.JumpVelocity, s.Velocity.Z);
                s.Phase = GroundedPhase.Ascending;
                // 消费缓冲与土狼窗口，防止上升段结束后残留窗口触发二段跳
                s.TimeSinceJumpPressed = config.InputBuffer + 1f;
                s.TimeSinceGrounded = config.CoyoteTime + 1f;
            }

            // --- 5. 垂直物理（按相位分派） ---
            if (s.Phase == GroundedPhase.Ascending)
            {
                // 可变跳跃：上升中松键 → 截断上升速度（30–50% 区间，取 retain 上沿）
                if (!input.JumpHeld && s.Velocity.Y > config.JumpVelocity * config.VariableJumpRetain)
                {
                    s.Velocity = new Vector3(s.Velocity.X, config.JumpVelocity * config.VariableJumpRetain, s.Velocity.Z);
                }
                s.Velocity = new Vector3(s.Velocity.X, s.Velocity.Y - config.UpGravity * dt, s.Velocity.Z);
                if (s.Velocity.Y <= 0f)
                {
                    s.Phase = GroundedPhase.Falling;
                }
            }
            if (s.Phase == GroundedPhase.Falling)
            {
                // 下落重力 = 上升重力 × FallGravityMult（干脆抛物线）
                s.Velocity = new Vector3(s.Velocity.X, s.Velocity.Y - config.UpGravity * config.FallGravityMult * dt, s.Velocity.Z);
            }

            // --- 6. 落地判定（外部 IsGrounded 回报 + 下降中） ---
            if (input.IsGrounded && s.Phase == GroundedPhase.Falling)
            {
                s.Velocity = new Vector3(s.Velocity.X, 0f, s.Velocity.Z);
                s.Phase = GroundedPhase.Grounded;
                s.TimeSinceGrounded = 0f;
            }
            if (input.IsGrounded && s.Phase == GroundedPhase.Grounded && s.Velocity.Y < 0f)
            {
                s.Velocity = new Vector3(s.Velocity.X, 0f, s.Velocity.Z);
            }

            // --- 7. 水平加速/减速（斜向归一化 → 对角线不超速；空中加速度 = 地面 × air control） ---
            Vector2 move = input.MoveXZ;
            float moveLen = move.Length();
            if (moveLen > 1f)
            {
                move /= moveLen; // 只钳制超过 1 的合成输入，保留模拟量半行程
            }
            bool stopping = moveLen <= 1e-6f;
            float rate = grounded
                ? (stopping ? config.GroundDecel : config.GroundAccel)
                : config.GroundAccel * config.AirControlMult;
            Vector2 target = move * config.MaxSpeed;
            Vector2 hv = new Vector2(s.Velocity.X, s.Velocity.Z);
            Vector2 diff = target - hv;
            float maxDelta = rate * dt;
            if (diff.Length() <= maxDelta)
            {
                hv = target;
            }
            else
            {
                hv += diff / diff.Length() * maxDelta; // MoveTowards 语义：不会减速过头（不减为负）
            }
            s.Velocity = new Vector3(hv.X, s.Velocity.Y, hv.Y);

            // --- 8. 视角增量（yaw/pitch，pitch 钳制防翻转） ---
            s.Yaw -= input.LookDelta.X * config.LookSensitivity;
            s.Pitch -= input.LookDelta.Y * config.LookSensitivity;
            if (s.Pitch > PitchLimitRad) s.Pitch = PitchLimitRad;
            if (s.Pitch < -PitchLimitRad) s.Pitch = -PitchLimitRad;

            // --- 9. 位置积分（y 由外部碰撞修正，Core 保持运动学一致） ---
            s.Position = new Vector3(
                s.Position.X + s.Velocity.X * dt,
                s.Position.Y + s.Velocity.Y * dt,
                s.Position.Z + s.Velocity.Z * dt);

            return s;
        }
    }
}
