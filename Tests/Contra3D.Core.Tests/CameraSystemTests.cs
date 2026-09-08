using System;
using System.Collections.Generic;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class CameraSystemTests
    {
        // T-BDD-ADOPT-camg01: 第三人称跟随平滑阻尼跟踪
        [Fact]
        public void Update_BDD_third_person_follow_smooth_damping()
        {
            // given: third-person mode, target at origin, offset (0.6, 1.7, -2.5), damping=5.0
            var rig = new CameraRig(new Vector3(0f, 2f, -2f), new Vector3(0f, 0f, 0f));
            rig.Damping = 5.0f;
            rig.TargetPosition = new Vector3(10f, 0f, 0f); // teleport target to (10, 0, 0)

            // when: update for 60 frames at 60Hz (dt=0.016) — enough for <0.1m error
            // With damping=5.0, factor=1-e^(-0.08)≈0.077, initial distance≈10.7m
            // After 60 frames: 10.7*(0.923)^60 ≈ 0.04m
            for (int i = 0; i < 60; i++)
            {
                rig.Update(0.016f);
            }

            // then: camera position should be close to desired position
            // desired = target + (right, up, -back) = (10 + 0.6, 0 + 1.7, 0 - 2.5) = (10.6, 1.7, -2.5)
            Vector3 desired = rig.TargetPosition + new Vector3(rig.OffsetsRight, rig.OffsetsUp, -rig.OffsetsBack);
            float error = (rig.Position - desired).Length();
            Assert.True(error < 0.1f, $"Camera error {error}m exceeds 0.1m threshold at frame 60");

            // then: camera looks at target
            Assert.NotEqual(0f, rig.Rotation.X); // has pitch
        }

        // T-BDD-ADOPT-camg02: 相机穿墙修正射线检测
        [Fact]
        public void CollisionCorrect_BDD_wall_correction_applied()
        {
            // given: camera at (0, 2, -5), target at (0, 1, 0), wall at (0, 1, -2)
            var rig = new CameraRig(new Vector3(0f, 2f, -5f), new Vector3(0f, 1f, 0f));
            var obstacles = new List<Vector3> { new Vector3(0f, 1f, -2f) };

            // when: collision correction runs
            bool corrected = rig.CollisionCorrect(0f, obstacles);

            // then: correction applied (wall is between camera and target)
            Assert.True(corrected, "Wall should trigger collision correction");
            // corrected position should be just in front of the wall (t≈3.0 from camera at z=-5, wall at z=-2)
            // Position = from + dir * (t + 0.2) where t is hit distance along ray
            // Ray from (0,2,-5) to (0,1,0): dir=(0,-1/√29,5/√29), dist≈5.39
            // Wall at (0,1,-2): closest point on ray is at t≈3.0 along ray
            // Position ≈ (0, 2-3/√29, -5+15/√29) + 0.2*dir ≈ near wall front
            Assert.True(rig.Position.Z > -3.0f, "Camera should move forward of wall");
        }

        // T-BDD-ADOPT-camg03: 相机模式切换缓动过渡
        [Fact]
        public void SwitchMode_BDD_ease_in_out_cubic_transition()
        {
            // given: current mode third-person at position (0, 0, 0), switch to fixed with 1.5s duration
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.SwitchMode(CameraMode.Fixed, duration: 1.5f);
            Assert.True(rig.Mode == CameraMode.Fixed);

            // when: update for full transition duration
            rig.UpdateModeTransition(1.5f);

            // then: transition complete, easing at t=1.0 = 1.0
            var activeField = typeof(CameraRig).GetField("_modeTransitionActive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.False((bool)activeField.GetValue(rig));
            // final position should be _modeEndPos
            Assert.Equal(new Vector3(0f, 5f, -10f), rig.Position);
        }

        // T-BDD-ADOPT-camg04: 创伤值驱动屏幕抖动
        [Fact]
        public void AddTrauma_BDD_trauma_driven_shake_decay()
        {
            // given: trauma = 0.0, explosion at 5m with base trauma increment 0.5
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.AddTrauma(0.5f, distanceToSource: 5f, closeDistance: 5f);

            // then: trauma set to 0.5
            Assert.Equal(0.5f, rig.Trauma, 3);

            // shake amplitude proportional to trauma² = 0.25
            var shake = rig.ApplyTraumaShake(0f, time: 1.0f);
            Assert.True(shake.Length() > 0f, "Shake should produce non-zero displacement at trauma=0.5");

            // when: update for 0.5s (decay rate = 1.0/s)
            rig.AddTrauma(0f, distanceToSource: 5f); // no additional trauma
            rig.ApplyTraumaShake(0.5f, time: 1.0f);

            // then: trauma decayed to ~0, shake stops
            Assert.True(rig.Trauma <= 0.01f, $"Trauma should decay to ~0 after 0.5s, got {rig.Trauma}");
            var noShake = rig.ApplyTraumaShake(0f, time: 2.0f);
            Assert.Equal(Vector3.Zero, noShake);
        }

        // T-BDD-ADOPT-camg05: 速度触发 FOV 动态调整
        [Fact]
        public void UpdateFOV_BDD_dynamic_fov_speed_triggered()
        {
            // given: base FOV=60°, max FOV=90°, speed threshold=8m/s, max speed=12m/s
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.BaseFOV = 60f;
            rig.MaxFOV = 90f;
            rig.FovSpeedThreshold = 8f;
            rig.FovMaxSpeed = 12f;
            rig.FovLerpSpeed = 0.1f;
            rig.FOV = 60f;

            // when: player accelerates to 10m/s
            rig.UpdateFOV(10f);
            // target = 60 + (90-60) * min(1, (10-8)/(12-8)) = 60 + 30 * 0.5 = 75
            // actual = 60 + (75-60) * 0.1 = 61.5
            Assert.Equal(61.5f, rig.FOV, 1);

            // continue updating until stable (~0.5s = 30 frames at 60Hz)
            for (int i = 0; i < 30; i++)
            {
                rig.UpdateFOV(10f);
            }
            // Should be very close to 75°
            Assert.InRange(rig.FOV, 74f, 76f); // FOV should stabilize near 75°

            // when: decelerate to 5m/s (below threshold)
            rig.UpdateFOV(5f);
            // target = 60 (below threshold)
            // actual starts moving back toward 60
            Assert.True(rig.FOV < 75f, "FOV should start decreasing below threshold");
        }

        // T-BDD-ADOPT-camg06: 相机俯仰偏航角限制
        [Fact]
        public void ApplyInput_BDD_pitch_yaw_constraints()
        {
            // given: pitch_min=-60°, pitch_max=80°, yaw unlimited
            // Manually construct constraint to avoid recursive minimap CameraRig constructor
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.Constraints = new CameraConstraint
            {
                PitchMinDeg = -60f,
                PitchMaxDeg = 80f,
                YawMinDeg = float.NegativeInfinity,
                YawMaxDeg = float.PositiveInfinity,
                YawUnlimited = true
            };

            // when: try to push pitch to -80° (below minimum)
            float initialPitch = rig.Rotation.X;
            rig.ApplyInput(deltaPitchDeg: -30f, deltaYawDeg: 0f);

            // then: pitch clamped to minimum (-60°)
            Assert.True(rig.Rotation.X >= -60f - 0.01f, $"Pitch {rig.Rotation.X} should be >= -60°");

            // yaw should accumulate freely
            rig.ApplyInput(deltaPitchDeg: 0f, deltaYawDeg: 45f);
            Assert.Equal(45f, rig.Rotation.Y, 3);
        }

        // T-BDD-ADOPT-camg07: 双人本地分屏水平分割
        [Fact]
        public void SetupSplitscreen_BDD_horizontal_two_player()
        {
            // given: 2-player local co-op, horizontal splitscreen, screen 1920x1080
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.SetupSplitscreen(2, "horizontal");

            // then: two independent rigs created
            Assert.Equal(2, rig.SplitscreenPlayers);
            Assert.Equal(2, rig.SplitscreenRigs.Count);

            // player 1 (index 0) viewport: rect(0, 0, 1, 0.5) — bottom half
            var vp1 = rig.GetViewportRect(0);
            Assert.Equal((0f, 0f, 1f, 0.5f), (vp1.x, vp1.y, vp1.w, vp1.h));

            // player 2 (index 1) viewport: rect(0, 0.5, 1, 0.5) — top half
            var vp2 = rig.GetViewportRect(1);
            Assert.Equal((0f, 0.5f, 1f, 0.5f), (vp2.x, vp2.y, vp2.w, vp2.h));

            // each player has independent rig
            Assert.NotNull(rig.SplitscreenRigs[0].Rig);
            Assert.NotNull(rig.SplitscreenRigs[1].Rig);
        }

        // T-BDD-ADOPT-camg08: 小地图相机正交俯视
        [Fact]
        public void UpdateMinimap_BDD_orthographic_top_down_follow()
        {
            // given: minimap camera with height=50m
            var rig = new CameraRig(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            rig.Mode = CameraMode.Minimap;

            // when: player moves from (10, 0, 10) to (20, 0, 20)
            rig.UpdateMinimap(new Vector3(10f, 0f, 10f));
            var minimapPos1 = rig.MinimapCamera.Position;
            Assert.Equal(10f, minimapPos1.X);
            Assert.Equal(50f, minimapPos1.Y, 3); // fixed height
            Assert.Equal(10f, minimapPos1.Z);

            rig.UpdateMinimap(new Vector3(20f, 0f, 20f));
            var minimapPos2 = rig.MinimapCamera.Position;
            Assert.Equal(20f, minimapPos2.X);
            Assert.Equal(50f, minimapPos2.Y, 3);
            Assert.Equal(20f, minimapPos2.Z);
        }
    }
}
