using System.Numerics;
using Xunit;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class CrosshairSolverTests
    {
        [Fact]
        public void WorldToScreenPoint_CenterTarget_ReturnsCenter()
        {
            var cam = new CameraParams(new Vector3(0, 2, -5), 0, 0, 60f);
            var result = CrosshairSolver.WorldToScreenPoint(new Vector3(0, 0, 0), cam);
            Assert.True(System.Math.Abs(result.X - 0.5f) < 0.01f, $"Expected X≈0.5, got {result.X}");
            Assert.True(System.Math.Abs(result.Y - 0.5f) < 0.01f, $"Expected Y≈0.5, got {result.Y}");
        }

        [Fact]
        public void GetCrosshairScreenPos_AlwaysReturnsCenter()
        {
            var cam = new CameraParams(new Vector3(0, 0, 0), 0, 0, 60f);
            var result = CrosshairSolver.GetCrosshairScreenPos(Vector3.UnitZ, cam);
            Assert.Equal(0.5f, result.X);
            Assert.Equal(0.5f, result.Y);
        }

        [Fact]
        public void GetCrosshairScreenPos_DifferentAimDirections_AllReturnCenter()
        {
            var cam = new CameraParams(new Vector3(0, 0, 0), 0, 0, 60f);
            // 不同瞄准方向均应返回屏幕中心
            var r1 = CrosshairSolver.GetCrosshairScreenPos(Vector3.UnitX, cam);
            var r2 = CrosshairSolver.GetCrosshairScreenPos(-Vector3.UnitY, cam);
            var r3 = CrosshairSolver.GetCrosshairScreenPos(new Vector3(1, 1, 1), cam);
            Assert.Equal(0.5f, r1.X); Assert.Equal(0.5f, r1.Y);
            Assert.Equal(0.5f, r2.X); Assert.Equal(0.5f, r2.Y);
            Assert.Equal(0.5f, r3.X); Assert.Equal(0.5f, r3.Y);
        }

        [Fact]
        public void WorldToScreenPoint_BehindCamera_ReturnsCenter()
        {
            var cam = new CameraParams(new Vector3(0, 0, 0), 0, 0, 60f);
            // 目标在相机后方 (0,0,-1) 相对于相机朝向 (0,0,-1) 的前方是正Z
            // 相机 forward = (0,0,-1)，目标在 (0,0,-10)，toTarget=(0,0,-10)，dot=10 > 0 所以在前方
            // 目标在 (0,0,5)，toTarget=(0,0,5)，dot=-5 <= 0 所以在后方
            var result = CrosshairSolver.WorldToScreenPoint(new Vector3(0, 0, 5), cam);
            Assert.Equal(0.5f, result.X);
            Assert.Equal(0.5f, result.Y);
        }

        [Fact]
        public void WorldToScreenPoint_OffCenterTarget_ReturnsOffset()
        {
            var cam = new CameraParams(new Vector3(0, 0, -5), 0, 0, 60f);
            // 目标在相机右侧 (3, 0, -5)
            var result = CrosshairSolver.WorldToScreenPoint(new Vector3(3, 0, -5), cam);
            // 右侧目标应使 X > 0.5
            Assert.True(result.X > 0.5f, $"Expected X>0.5 for right-side target, got {result.X}");
            // Y 应保持 0.5（同一高度）
            Assert.True(System.Math.Abs(result.Y - 0.5f) < 0.01f, $"Expected Y≈0.5, got {result.Y}");
        }
    }
}
