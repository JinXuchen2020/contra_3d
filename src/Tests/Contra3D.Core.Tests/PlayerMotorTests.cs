using System;
using System.Numerics;
using Contra3D.Core;
using Xunit;

namespace Contra3D.Core.Tests
{
    /// <summary>
    /// T-SYS-002 player_movement Core 单测（templates/system_design/player_movement.md 测试策略 10 例）。
    /// </summary>
    public class PlayerMotorTests
    {
        private static readonly MotorConfig Cfg = MotorConfig.Default();
        private const float Dt = 0.01f; // 100Hz 固定步长，确定性

        private static MotorState Run(MotorState s, MotorInput input, float dt = Dt)
        {
            return PlayerMotor.Simulate(s, input, Cfg, dt);
        }

        private static MotorInput Idle(bool grounded = true)
        {
            MotorInput i = new MotorInput();
            i.MoveXZ = Vector2.Zero;
            i.LookDelta = Vector2.Zero;
            i.IsGrounded = grounded;
            i.JumpHeld = false;
            return i;
        }

        private static MotorInput MoveForward(float x = 0f, float z = 1f)
        {
            MotorInput i = Idle();
            i.MoveXZ = new Vector2(x, z);
            return i;
        }

        // --- 例1: 移动向量归一化，斜向不超速 ---
        [Fact]
        public void DiagonalInput_SpeedMagnitudeEqualsMaxSpeed()
        {
            MotorState s = MotorState.Initial(0f);
            for (int f = 0; f < 200; f++)
            {
                s = Run(s, MoveForward(1f, 1f));
            }
            float planar = MathF.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z);
            Assert.Equal(Cfg.MaxSpeed, planar, 3);
        }

        // --- 例2: 零输入减速至 0，不减为负（GroundDecel 曲线） ---
        [Fact]
        public void ZeroInput_DeceleratesToZeroNeverNegative()
        {
            MotorState s = MotorState.Initial(0f);
            for (int f = 0; f < 200; f++) s = Run(s, MoveForward()); // 先满速

            float prev = MathF.Abs(s.Velocity.Z);
            bool reachedZero = false;
            for (int f = 0; f < 300; f++)
            {
                s = Run(s, Idle());
                float planar = MathF.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z);
                Assert.True(planar <= prev + 1e-5f, "deceleration must be monotonic");
                Assert.True(planar >= 0f, "speed must never go negative");
                prev = planar;
                if (planar == 0f) { reachedZero = true; break; }
            }
            Assert.True(reachedZero, "must reach exactly zero via MoveTowards clamp");
        }

        // --- 例3: 满输入收敛时间 = MaxSpeed/GroundAccel (7/40 = 0.175s, ±5%) ---
        [Fact]
        public void FullInput_ConvergesWithinExpectedTime()
        {
            MotorState s = MotorState.Initial(0f);
            float expected = Cfg.MaxSpeed / Cfg.GroundAccel; // 0.175s
            float t = 0f;
            while (MathF.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z) < Cfg.MaxSpeed * 0.999f && t < 2f)
            {
                s = Run(s, MoveForward());
                t += Dt;
            }
            Assert.InRange(t, expected * 0.95f, expected * 1.05f);
        }

        // --- 例4: 空中加速度 = 地面 × airControl（含边界 0.4/0.8） ---
        [Theory]
        [InlineData(0.6f, 24f)]
        [InlineData(0.4f, 16f)]
        [InlineData(0.8f, 32f)]
        public void AirControl_AccelIsGroundTimesMult(float mult, float expectedAccel)
        {
            MotorConfig c = Cfg;
            c.AirControlMult = mult;
            MotorState s = MotorState.Initial(0f);
            s.Phase = GroundedPhase.Falling; // 空中
            MotorInput i = MoveForward();
            i.IsGrounded = false;
            s = PlayerMotor.Simulate(s, i, c, 0.1f);
            Assert.Equal(expectedAccel * 0.1f, MathF.Abs(s.Velocity.Z), 3);
        }

        // --- 例5: coyote 上边界（默认 120ms；119ms 成功 / 150ms 失败） ---
        // 直接构造 TimeSinceGrounded（避免 float 累加误差穿过边界）；press 帧开头会再累计 fine。
        [Theory]
        [InlineData(0.119f, true)]
        [InlineData(0.150f, false)]
        public void CoyoteTime_Boundary(float airborneFor, bool shouldJump)
        {
            const float fine = 0.001f;
            MotorState s = MotorState.Initial(0f);
            s = Run(s, Idle()); // 接地一帧 (TimeSinceGrounded=0)

            // 离地 airborneFor 秒后按下跳跃（press 帧内 TimeSinceGrounded 达到 airborneFor）
            MotorInput press = Idle(false);
            press.JumpPressed = true;
            press.JumpHeld = true;
            var pre = s;
            pre.TimeSinceGrounded = airborneFor - fine;
            s = Run(pre, press, fine);

            if (shouldJump)
            {
                Assert.Equal(GroundedPhase.Ascending, s.Phase);
                Assert.Equal(Cfg.JumpVelocity - Cfg.UpGravity * fine, s.Velocity.Y, 3); // 当帧已减一帧重力
            }
            else
            {
                Assert.NotEqual(GroundedPhase.Ascending, s.Phase);
            }
        }

        // --- 例6: 输入缓冲（落地前 100ms 按跳 → 落地瞬间执行；200ms 前 → 丢弃） ---
        [Theory]
        [InlineData(0.100f, true)]
        [InlineData(0.200f, false)]
        public void JumpBuffer_Boundary(float pressBeforeLanding, bool shouldLandJump)
        {
            MotorState s = MotorState.Initial(0f);
            s.Phase = GroundedPhase.Falling; // 空中下落
            s.Velocity = new Vector3(0f, -5f, 0f);
            s.TimeSinceGrounded = 1f; // coyote 窗口外：确保空中按跳不会经 coyote 通道立即起跳

            // 按跳（不接地）
            MotorInput press = Idle(false);
            press.JumpPressed = true;
            press.JumpHeld = true;
            s = Run(s, press);

            // 再过 pressBeforeLanding 秒落地
            MotorInput air = Idle(false);
            float elapsed = 0f;
            while (elapsed < pressBeforeLanding - Dt * 0.5f)
            {
                s = Run(s, air);
                elapsed += Dt;
            }
            MotorInput land = Idle(true);
            land.JumpHeld = true; // 按跳落地：起跳当帧键仍在按住，不触发可变跳跃截断
            s = Run(s, land);

            if (shouldLandJump)
            {
                // 落地帧起跳，Ascending 分支当帧已减一帧重力
                Assert.Equal(Cfg.JumpVelocity - Cfg.UpGravity * Dt, s.Velocity.Y, 3);
                Assert.Equal(GroundedPhase.Ascending, s.Phase);
            }
            else
            {
                Assert.Equal(0f, s.Velocity.Y, 3);
                Assert.Equal(GroundedPhase.Grounded, s.Phase);
            }
        }

        // --- 例7: buffer 消费后清空，不二次触发 ---
        [Fact]
        public void Buffer_ConsumedAfterExecution_NoDoubleJump()
        {
            // 复用例6成功路径：空中按跳 → 落地瞬间经 buffer 通道起跳
            MotorState s = MotorState.Initial(0f);
            s.Phase = GroundedPhase.Falling;
            s.Velocity = new Vector3(0f, -5f, 0f);
            s.TimeSinceGrounded = 1f; // coyote 窗口外（见例6）

            MotorInput press = Idle(false);
            press.JumpPressed = true;
            press.JumpHeld = true;
            s = Run(s, press); // 空中按跳：coyote 外，不当帧起跳，仅进入缓冲

            for (int f = 0; f < 9; f++) s = Run(s, Idle(false));

            MotorInput land = Idle(true);
            land.JumpHeld = true;
            s = Run(s, land); // 落地帧经缓冲通道起跳（当帧已减一帧重力）
            Assert.Equal(Cfg.JumpVelocity - Cfg.UpGravity * Dt, s.Velocity.Y, 3);

            MotorInput held = Idle(false);
            held.JumpHeld = true;
            // 上升段逐帧检查：速度只受重力递减，不会再次回到 JumpVelocity
            for (int f = 0; f < 40; f++)
            {
                s = Run(s, held);
                Assert.True(s.Velocity.Y < Cfg.JumpVelocity, "no second jump impulse after buffer consumed");
            }
        }

        // --- 例8: 可变跳跃——上升中松键截断至 30–50% 区间 ---
        [Fact]
        public void VariableJump_CutToRetainRatio()
        {
            MotorState s = MotorState.Initial(0f);
            MotorInput press = Idle();
            press.JumpPressed = true;
            press.JumpHeld = true;
            s = Run(s, press);
            Assert.Equal(GroundedPhase.Ascending, s.Phase);

            MotorInput released = Idle();
            released.JumpHeld = false;
            s = Run(s, released);

            // 截断至 retain 比例，且当帧仍减一帧上升重力
            Assert.Equal(Cfg.JumpVelocity * 0.5f - Cfg.UpGravity * Dt, s.Velocity.Y, 3);
            float ratio = s.Velocity.Y / Cfg.JumpVelocity;
            Assert.InRange(ratio, 0.30f, 0.50f);
        }

        // --- 例9: 下落重力 = 上升重力 × FallGravityMult ---
        [Fact]
        public void FallGravity_EqualsUpGravityTimesMult()
        {
            MotorState s = MotorState.Initial(0f);
            s.Phase = GroundedPhase.Falling;
            s.Velocity = new Vector3(0f, -1f, 0f);
            s = Run(s, Idle(false), 0.1f);
            float expected = -1f - Cfg.UpGravity * Cfg.FallGravityMult * 0.1f;
            Assert.Equal(expected, s.Velocity.Y, 4);
        }

        // --- 例10: 确定性——同输入序列两次 Simulate 输出逐帧一致（容差 1e-4） ---
        [Fact]
        public void Determinism_SameInputSequence_SameOutput()
        {
            MotorState a = MotorState.Initial(0f);
            MotorState b = MotorState.Initial(0f);
            var rng = new Random(42);
            for (int f = 0; f < 500; f++)
            {
                MotorInput i = new MotorInput();
                i.MoveXZ = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
                i.LookDelta = new Vector2((float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 10f);
                i.IsGrounded = rng.NextDouble() > 0.3;
                i.JumpPressed = rng.NextDouble() > 0.9;
                i.JumpHeld = rng.NextDouble() > 0.5;
                a = Run(a, i);
                b = Run(b, i);
                Assert.InRange(MathF.Abs(a.Position.X - b.Position.X), 0f, 1e-4f);
                Assert.InRange(MathF.Abs(a.Position.Y - b.Position.Y), 0f, 1e-4f);
                Assert.InRange(MathF.Abs(a.Position.Z - b.Position.Z), 0f, 1e-4f);
                Assert.Equal(a.Phase, b.Phase);
            }
        }
    }
}
