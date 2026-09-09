namespace Contra3D.Core
{
    /// <summary>渲染/输入相关可调参数集中配置。</summary>
    public readonly struct RenderConfig
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public int TargetFps { get; }
        public float MouseSensitivityDeg { get; }
        public float CameraDampingSec { get; }

        public RenderConfig(int targetWidth, int targetHeight, int targetFps,
                           float mouseSensitivityDeg, float cameraDampingSec)
        {
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            TargetFps = targetFps;
            MouseSensitivityDeg = mouseSensitivityDeg;
            CameraDampingSec = cameraDampingSec;
        }
    }
}
