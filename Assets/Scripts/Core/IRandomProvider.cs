using System;

namespace Contra3D.Core
{
    /// <summary>
    /// 随机数提供者接口，用于确定性测试。
    /// 生产环境使用 <see cref="DefaultRandomProvider"/>，测试可注入确定性实现。
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>返回 [0.0, 1.0) 区间的随机双精度浮点数。</summary>
        double NextDouble();

        /// <summary>返回 [min, max) 区间的随机单精度浮点数。</summary>
        float NextFloat(float min, float max);
    }

    /// <summary>
    /// 默认随机数提供者实现，使用共享的 <see cref="Random"/> 实例。
    /// </summary>
    public sealed class DefaultRandomProvider : IRandomProvider
    {
        private static readonly Random _shared = new Random();

        /// <inheritdoc />
        public double NextDouble() => _shared.NextDouble();

        /// <inheritdoc />
        public float NextFloat(float min, float max) => (float)_shared.NextDouble() * (max - min) + min;
    }

    /// <summary>
    /// 确定性随机数提供者，用于测试。使用固定种子产生可复现序列。
    /// </summary>
    public sealed class DeterministicRandomProvider : IRandomProvider
    {
        private readonly Random _random;

        /// <summary>使用指定种子创建确定性提供者。</summary>
        public DeterministicRandomProvider(int seed = 42)
        {
            _random = new Random(seed);
        }

        /// <inheritdoc />
        public double NextDouble() => _random.NextDouble();

        /// <inheritdoc />
        public float NextFloat(float min, float max) => (float)_random.NextDouble() * (max - min) + min;
    }
}