namespace Contra3D.Core
{
    /// <summary>
    /// Post-processing pipeline 配置。纯 C# 结构，零 UnityEngine 依赖，
    /// 由 Contra3D.Runtime 层映射到 URP Volume Profile components。
    /// </summary>
    public readonly struct PostProcessingConfig
    {
        public bool BloomEnabled { get; }
        public float BloomIntensity { get; }
        public bool ColorAdjustmentsEnabled { get; }
        public float ExposureCompensation { get; }
        public bool VignetteEnabled { get; }
        public float VignetteIntensity { get; }

        public PostProcessingConfig(bool bloomEnabled, float bloomIntensity,
                                    bool colorAdjustmentsEnabled, float exposureCompensation,
                                    bool vignetteEnabled, float vignetteIntensity)
        {
            BloomEnabled = bloomEnabled;
            BloomIntensity = bloomIntensity;
            ColorAdjustmentsEnabled = colorAdjustmentsEnabled;
            ExposureCompensation = exposureCompensation;
            VignetteEnabled = vignetteEnabled;
            VignetteIntensity = vignetteIntensity;
        }

        /// <summary>默认 post-processing：Bloom 轻度，Vignette 轻度。</summary>
        public static PostProcessingConfig Default =>
            new PostProcessingConfig(bloomEnabled: true, bloomIntensity: 0.5f,
                                     colorAdjustmentsEnabled: true, exposureCompensation: 0.0f,
                                     vignetteEnabled: true, vignetteIntensity: 0.3f);
    }
}
