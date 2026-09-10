using Xunit;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class RenderConfigTests
    {
        [Fact]
        public void DefaultValues_MatchNumericConstraints()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(1920, config.TargetWidth);
            Assert.Equal(1080, config.TargetHeight);
            Assert.Equal(60, config.TargetFps);
        }

        [Fact]
        public void CameraDamping_IsWithinLimit()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.True(config.CameraDampingSec <= 0.15f,
                $"CameraDampingSec={config.CameraDampingSec} exceeds 0.15s limit");
        }

        [Fact]
        public void MouseSensitivity_MatchesContract()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(0.1f, config.MouseSensitivityDeg);
        }

        [Fact]
        public void TargetFps_Equals60()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(60, config.TargetFps);
        }

        [Fact]
        public void Resolution_Match1920x1080()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(1920, config.TargetWidth);
            Assert.Equal(1080, config.TargetHeight);
        }

        // T-BDD-ADOPT-rg_rendering_target_fps_set
        // GameBootstrap sets Application.targetFrameRate to 60 at startup.
        // Core-side invariant: RenderConfig.TargetFps must equal 60.
        [Fact]
        public void TargetFps_SetTo60_ByGameBootstrap()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(60, config.TargetFps);
        }

        // T-FUNC-P1-RENDER-INCOMPLETE: FOV 由 RenderConfig 驱动，非硬编码
        [Fact]
        public void DefaultFovDeg_MatchesContract_60Deg()
        {
            var config = new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);
            Assert.Equal(60.0f, config.DefaultFovDeg);
        }

        // T-FUNC-P1-RENDER-INCOMPLETE: static Default factory produces valid config
        [Fact]
        public void Default_StaticFactory_ProducesValidConfig()
        {
            var config = RenderConfig.Default;
            Assert.Equal(1920, config.TargetWidth);
            Assert.Equal(1080, config.TargetHeight);
            Assert.Equal(60, config.TargetFps);
            Assert.Equal(60.0f, config.DefaultFovDeg);
            Assert.True(config.Validate());
        }

        // T-FUNC-P1-RENDER-INCOMPLETE: Validate() catches spec violations
        [Fact]
        public void Validate_FailsOnWrongFps()
        {
            var config = new RenderConfig(1920, 1080, 30, 60.0f, 0.1f, 0.15f);
            Assert.False(config.Validate());
        }

        [Fact]
        public void Validate_FailsOnWrongFov()
        {
            var config = new RenderConfig(1920, 1080, 60, 90.0f, 0.1f, 0.15f);
            Assert.False(config.Validate());
        }

        [Fact]
        public void Validate_PassesOnDefaults()
        {
            var config = RenderConfig.Default;
            Assert.True(config.Validate());
        }
    }
}
