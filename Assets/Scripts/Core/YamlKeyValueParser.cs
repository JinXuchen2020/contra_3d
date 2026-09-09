using System;

namespace Contra3D.Core
{
    /// <summary>YAML 单行 key:value 解析工具。</summary>
    public static class YamlKeyValueParser
    {
        /// <summary>从已裁剪的 YAML 行中提取值（去除引号）。例："name: Mario" → "Mario"。</summary>
        public static string ParseValue(string line, string prefix)
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                return null;
            return line.Substring(prefix.Length).Trim().Trim('"').Trim('\'');
        }

        /// <summary>从已裁剪的 YAML 行中提取值（去除引号，转小写）。</summary>
        public static string ParseValueLower(string line, string prefix)
        {
            string val = ParseValue(line, prefix);
            return val == null ? null : val.ToLowerInvariant();
        }
    }
}
