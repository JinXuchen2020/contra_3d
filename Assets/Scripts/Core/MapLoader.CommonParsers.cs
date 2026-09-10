// T-SYS-007 (map_loading/Core) — Common parsing utilities (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Common Parsers

        private static void ParseInlineObj(string line, Dictionary<string, string> dict)
        {
            // Remove leading "- {" and trailing "}"
            string inner = line.Substring(2).Trim(); // strip "- "
            if (inner.StartsWith("{"))
                inner = inner.Substring(1);
            if (inner.EndsWith("}"))
                inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();

            // Split respecting bracket nesting so vector literals like [0, 1, 0] stay intact
            var parts = SplitPreservingBrackets(inner);
            foreach (string part in parts)
            {
                string p = part.Trim();
                int ci = p.IndexOf(':');
                if (ci < 0)
                    continue;
                string key = p.Substring(0, ci).Trim();
                string val = p.Substring(ci + 1).Trim().Trim('"').Trim('\'');
                dict[key] = val;
            }
        }

        /// <summary>Splits a comma-separated string into parts, respecting [...] and {...} nesting.</summary>
        private static string[] SplitPreservingBrackets(string inner)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(inner.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(inner.Substring(start));
            return result.ToArray();
        }

        private static float ParseFloat(Dictionary<string, string> f, string key, float fallback)
        {
            if (!f.TryGetValue(key, out var s) || string.IsNullOrEmpty(s))
                return fallback;
            float.TryParse(s, out float v);
            return v;
        }

        #endregion
    }
}
