namespace Contra3D.Core
{
    /// <summary>渲染/输入相关可调参数集中配置。FOV 由 DefaultFovDeg 驱动，禁止散落硬编码。</summary>
    public readonly struct RenderConfig
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public int TargetFps { get; }
        public float DefaultFovDeg { get; }
        public float MouseSensitivityDeg { get; }
        public float CameraDampingSec { get; }

        public RenderConfig(int targetWidth, int targetHeight, int targetFps,
                            float defaultFovDeg, float mouseSensitivityDeg, float cameraDampingSec)
        {
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            TargetFps = targetFps;
            DefaultFovDeg = defaultFovDeg;
            MouseSensitivityDeg = mouseSensitivityDeg;
            CameraDampingSec = cameraDampingSec;
        }

        /// <summary>设计契约 T-SYS-001 验收默认值 (1920x1080 / 60fps / 60 deg FOV / 0.1 deg 灵敏度 / 0.15s 阻尼)。</summary>
        public static RenderConfig Default => new RenderConfig(1920, 1080, 60, 60.0f, 0.1f, 0.15f);

        /// <summary>验证是否符合 design_contracts/rendering.yaml 数值约束。</summary>
        public bool Validate()
        {
            if (TargetWidth != 1920 || TargetHeight != 1080) return false;
            if (TargetFps != 60) return false;
            if (DefaultFovDeg != 60.0f) return false;
            if (MouseSensitivityDeg != 0.1f) return false;
            if (CameraDampingSec > 0.15f) return false;
            return true;
        }
    }
}
