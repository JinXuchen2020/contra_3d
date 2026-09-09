using System;
using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>向量扩展方法。</summary>
    public static class VectorExtensions
    {
        /// <summary>将随机散布偏移应用到方向向量。</summary>
        public static Vector3 ApplySpread(this Vector3 direction, float spreadDeg, IRandomProvider random)
        {
            if (spreadDeg <= 0f) return direction;
            float spreadRad = spreadDeg * (float)Math.PI / 180f;
            float dx = (float)(random.NextDouble() * 2 - 1) * spreadRad;
            float dz = (float)(random.NextDouble() * 2 - 1) * spreadRad;
            return Vector3.Normalize(new Vector3(direction.X + dx, direction.Y, direction.Z + dz));
        }
    }
}
