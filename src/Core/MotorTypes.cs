using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>
    /// 跳跃状态机相位（templates/system_design/player_movement.md 分层设计）。
    /// Grounded → Ascending → Falling → Grounded。
    /// </summary>
    public enum GroundedPhase
    {
        Grounded,
        Ascending,
        Falling
    }

    /// <summary>
    /// 移动手感参数集。全部数值出自 genre_knowledge/shooter_base.yaml
    /// （movement_feel / input_response 节，见 templates/system_design/player_movement.md 量化参数表）。
    /// 禁止在逻辑代码中散落魔法数，调参只改本结构。
    /// </summary>
    public struct MotorConfig
    {
        /// <summary>最大平面速度 m/s（范围 5–10）。</summary>
        public float MaxSpeed;

        /// <summary>地面加速 m/s²（范围 20–60）。</summary>
        public float GroundAccel;

        /// <summary>地面减速 m/s²（≈1.5× 加速 → 停得干脆；范围 30–100）。</summary>
        public float GroundDecel;

        /// <summary>空中控制系数（空中加速度 = 地面 × 本系数；范围 0.4–0.8）。</summary>
        public float AirControlMult;

        /// <summary>跳跃高度 m（范围 1.5–3.0）。</summary>
        public float JumpHeight;

        /// <summary>设计滞空时长 s（范围 0.6–1.2），用于反解起跳初速。</summary>
        public float Airtime;

        /// <summary>下落重力倍率（下落重力 > 上升重力 → 干脆抛物线；范围 1.2–2.0）。</summary>
        public float FallGravityMult;

        /// <summary>可变跳跃截断保留比（松键截断上升速度至 30–50%）。</summary>
        public float VariableJumpRetain;

        /// <summary>土狼时间 s（范围 0.080–0.150）。</summary>
        public float CoyoteTime;

        /// <summary>跳跃输入缓冲 s（范围 0.100–0.200）。</summary>
        public float InputBuffer;

        /// <summary>鼠标视角灵敏度（rad per count）。</summary>
        public float LookSensitivity;

        /// <summary>默认配置（player_movement.md 量化参数表基准值）。</summary>
        public static readonly MotorConfig Default = new MotorConfig
        {
            MaxSpeed = 7f,
            GroundAccel = 40f,
            GroundDecel = 60f,
            AirControlMult = 0.6f,
            JumpHeight = 2.2f,
            Airtime = 0.85f,
            FallGravityMult = 1.5f,
            VariableJumpRetain = 0.5f,
            CoyoteTime = 0.120f,
            InputBuffer = 0.150f,
            LookSensitivity = 0.002f,
        };

        /// <summary>
        /// 从 YAML 字符串加载配置。null 或空字符串时回退到内置默认值。
        /// 格式：每行 "key: value"，支持的 key 见属性注释。
        /// </summary>
        public static MotorConfig LoadFromYaml(string yamlContent)
        {
            var c = Default();  // 先获取默认值，避免递归
            if (string.IsNullOrWhiteSpace(yamlContent)) return c;

            foreach (var line in yamlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                var parts = trimmed.Split(':', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var val = parts[1].Trim();
                if (float.TryParse(val, out float fv))
                {
                    switch (key)
                    {
                        case "max_speed": c.MaxSpeed = fv; break;
                        case "ground_accel": c.GroundAccel = fv; break;
                        case "ground_decel": c.GroundDecel = fv; break;
                        case "air_control_mult": c.AirControlMult = fv; break;
                        case "jump_height": c.JumpHeight = fv; break;
                        case "airtime": c.Airtime = fv; break;
                        case "fall_gravity_mult": c.FallGravityMult = fv; break;
                        case "variable_jump_retain": c.VariableJumpRetain = fv; break;
                        case "coyote_time": c.CoyoteTime = fv; break;
                        case "input_buffer": c.InputBuffer = fv; break;
                        case "look_sensitivity": c.LookSensitivity = fv; break;
                    }
                }
            }
            return c;
        }

        /// <summary>
        /// 起跳初速：v0 = 2h / t_up，t_up = Airtime / 2（上升段反解，player_movement.md 参数表注）。
        /// </summary>
        public float JumpVelocity
        {
            get { float tUp = Airtime * 0.5f; return 2f * JumpHeight / tUp; }
        }

        /// <summary>上升重力 g = v0 / t_up（与 JumpVelocity 联动校验）。</summary>
        public float UpGravity
        {
            get { float tUp = Airtime * 0.5f; return JumpVelocity / tUp; }
        }
    }

    /// <summary>单帧输入快照（由 Runtime 层回填，Core 只读）。</summary>
    public struct MotorInput
    {
        /// <summary>平面移动轴（x=右, y=前），支持模拟量半行程；斜向超 1 由 Core 归一化。</summary>
        public Vector2 MoveXZ;

        /// <summary>鼠标增量（count）。</summary>
        public Vector2 LookDelta;

        /// <summary>本帧是否为跳跃按下沿（事件，非持续）。</summary>
        public bool JumpPressed;

        /// <summary>跳跃键当前是否按住（可变跳跃用）。</summary>
        public bool JumpHeld;

        /// <summary>外部接地状态（CharacterController.isGrounded 回填）。</summary>
        public bool IsGrounded;
    }

    /// <summary>
    /// 模拟输出状态。计时器以 dt 累计（帧率无关），Position 中的 y 由外部碰撞修正，
    /// Core 层不感知碰撞体（禁止回边：Core 不依赖 UnityEngine）。
    /// </summary>
    public struct MotorState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Pitch;
        public GroundedPhase Phase;

        /// <summary>距上次接地的时间 s（coyote 计时）。</summary>
        public float TimeSinceGrounded;

        /// <summary>距上次跳跃按下沿的时间 s（buffer 计时）。</summary>
        public float TimeSinceJumpPressed;

        public static MotorState Initial(float yaw)
        {
            MotorState s = new MotorState();
            s.Position = Vector3.Zero;
            s.Velocity = Vector3.Zero;
            s.Yaw = yaw;
            s.Pitch = 0f;
            s.Phase = GroundedPhase.Grounded;
            s.TimeSinceGrounded = 0f;
            s.TimeSinceJumpPressed = float.MaxValue;
            return s;
        }
    }
}
