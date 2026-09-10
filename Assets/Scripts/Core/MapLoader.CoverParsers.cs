// T-SYS-007 (map_loading/Core) — Cover point parsing logic (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Cover Parsers

        private static CoverPoint ParseCoverPoint(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            if (f.TryGetValue("facing_normal", out var fn))
            {
                var components = ParseVector3Components(fn);
                return new CoverPoint(x, y, z, components[0], components[1], components[2]);
            }
            return new CoverPoint(x, y, z);
        }

        /// <summary>
        /// 解析 facing_normal 字段，支持 "[0, 1, 0]" 或 "0,1,0" 格式。
        /// </summary>
        private static float[] ParseVector3Components(string value)
        {
            // Strip optional brackets
            string inner = value.Trim();
            if (inner.StartsWith("[")) inner = inner.Substring(1);
            if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();

            string[] parts = inner.Split(',');
            var result = new float[3];
            for (int i = 0; i < 3; i++)
            {
                float.TryParse(parts[i].Trim(), out result[i]);
            }
            return result;
        }

        #endregion
    }
}
