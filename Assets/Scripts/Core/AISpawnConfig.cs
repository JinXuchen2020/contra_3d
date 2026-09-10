using System;
using System.Collections.Generic;
using System.IO;

namespace Contra3D.Core
{
    /// <summary>
    /// AI 生成配置 — 可通过 YAML 加载，支持不同难度模式。
    /// </summary>
    public sealed class AISpawnConfig
    {
        /// <summary>普通敌人最大同时存活数量。</summary>
        public int MaxNormal { get; private set; } = 12;

        /// <summary>冲锋型敌人最大同时存活数量。</summary>
        public int MaxRusher { get; private set; } = 4;

        /// <summary>防门口霸营距离（米）— 生成点距离玩家小于此值将被拒绝。</summary>
        public float AntiDoorCampingDistance { get; private set; } = 5f;

        /// <summary>创建默认配置。</summary>
        public static AISpawnConfig Default => new AISpawnConfig();

        /// <summary>
        /// 从 YAML 文件加载配置。
        /// </summary>
        /// <param name="yamlPath">YAML 文件路径。</param>
        /// <returns>解析后的配置对象。</returns>
        /// <exception cref="FileNotFoundException">文件不存在。</exception>
        /// <exception cref="AISpawnConfigLoadException">解析或校验失败。</exception>
        public static AISpawnConfig Load(string yamlPath)
        {
            if (!File.Exists(yamlPath))
                throw new FileNotFoundException($"AI spawn config YAML not found: {yamlPath}");

            string content = File.ReadAllText(yamlPath);
            return LoadFromString(content);
        }

        /// <summary>
        /// 从 YAML 字符串加载配置。
        /// </summary>
        public static AISpawnConfig LoadFromString(string yamlContent)
        {
            var config = ParseConfig(yamlContent);
            ValidateConfig(config);
            return config;
        }

        private static AISpawnConfig ParseConfig(string yamlContent)
        {
            var result = new AISpawnConfig();
            string[] lines = yamlContent.Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("max_normal:"))
                {
                    string val = line.Substring("max_normal:".Length).Trim();
                    int.TryParse(val, out int v);
                    result.MaxNormal = v;
                }
                else if (line.StartsWith("max_rusher:"))
                {
                    string val = line.Substring("max_rusher:".Length).Trim();
                    int.TryParse(val, out int v);
                    result.MaxRusher = v;
                }
                else if (line.StartsWith("anti_door_camping_distance:"))
                {
                    string val = line.Substring("anti_door_camping_distance:".Length).Trim();
                    float.TryParse(val, out float v);
                    result.AntiDoorCampingDistance = v;
                }
            }

            return result;
        }

        private static void ValidateConfig(AISpawnConfig config)
        {
            var errors = new List<string>();

            if (config.MaxNormal <= 0)
                errors.Add("max_normal must be > 0");
            if (config.MaxRusher <= 0)
                errors.Add("max_rusher must be > 0");
            if (config.MaxRusher > config.MaxNormal)
                errors.Add("max_rusher cannot exceed max_normal");
            if (config.AntiDoorCampingDistance < 0f)
                errors.Add("anti_door_camping_distance must be >= 0");

            if (errors.Count > 0)
                throw new AISpawnConfigLoadException(errors);
        }

        /// <summary>AI 生成配置加载/校验异常。</summary>
        public sealed class AISpawnConfigLoadException : Exception
        {
            public List<string> Errors { get; }

            public AISpawnConfigLoadException(List<string> errors)
                : base(BuildMessage(errors))
            {
                Errors = errors ?? throw new ArgumentException("Errors must not be null.", nameof(errors));
            }

            private static string BuildMessage(List<string> errors)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("AI spawn config validation failed with the following errors:");
                foreach (var e in errors)
                    sb.AppendLine($"  - {e}");
                return sb.ToString();
            }
        }
    }
}