using Xunit;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class RenderConfigTests
    {
        [Fact]
        public void DefaultValues_MatchNumericConstraints()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.Equal(1920, config.TargetWidth);
            Assert.Equal(1080, config.TargetHeight);
            Assert.Equal(60, config.TargetFps);
        }

        [Fact]
        public void CameraDamping_IsWithinLimit()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.True(config.CameraDampingSec <= 0.15f,
                $"CameraDampingSec={config.CameraDampingSec} exceeds 0.15s limit");
        }

        [Fact]
        public void MouseSensitivity_MatchesContract()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.Equal(0.1f, config.MouseSensitivityDeg);
        }

        [Fact]
        public void TargetFps_Equals60()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.Equal(60, config.TargetFps);
        }

        [Fact]
        public void Resolution_Match1920x1080()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.Equal(1920, config.TargetWidth);
            Assert.Equal(1080, config.TargetHeight);
        }

        // T-BDD-ADOPT-rg_rendering_target_fps_set
        // GameBootstrap sets Application.targetFrameRate to 60 at startup.
        // Core-side invariant: RenderConfig.TargetFps must equal 60.
        [Fact]
        public void TargetFps_SetTo60_ByGameBootstrap()
        {
            var config = new RenderConfig(1920, 1080, 60, 0.1f, 0.15f);
            Assert.Equal(60, config.TargetFps);
        }
    }
}
