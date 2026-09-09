using System;
using System.Collections.Generic;
using System.IO;

namespace Contra3D.Core
{
    /// <summary>
    /// 从 YAML 文件加载敌人定义（ID + 击杀得分）。使用简单文本解析（不引入新依赖）。
    /// 仅用于初始化阶段（启动/关卡加载），不在运行时热路径中调用。
    /// </summary>
    /// <remarks>
    /// LOAD-TIME ONLY: This class performs YAML parsing with string.Split allocations.
    /// Must only be called at startup/level load, NEVER in runtime hot paths.
    /// </remarks>
    public static class EnemyLoader
    {
        /// <summary>默认敌人得分回退值（当 YAML 未提供 score 字段时）。</summary>
        public const int DefaultScore = 50;

        /// <summary>敌人 YAML 文件的相对路径（相对于项目根目录）。</summary>
        public const string EnemiesYamlPath = "data/enemies/enemies.yaml";

        /// <summary>加载结果：敌人 ID → 击杀得分映射。</summary>
        public readonly struct LoadResult
        {
            public readonly Dictionary<string, int> ScoreTable;

            public LoadResult(Dictionary<string, int> scoreTable)
            {
                ScoreTable = scoreTable;
            }
        }

        /// <summary>
        /// 从 enemies.yaml 文件加载分数表。
        /// </summary>
        /// <param name="yamlPath">YAML 文件路径。</param>
        /// <returns>敌人 ID → 击杀得分映射。</returns>
        /// <exception cref="FileNotFoundException">文件不存在。</exception>
        public static LoadResult Load(string yamlPath)
        {
            if (!File.Exists(yamlPath))
                throw new FileNotFoundException($"Enemies YAML not found: {yamlPath}");

            string content = File.ReadAllText(yamlPath);
            return LoadFromString(content);
        }

        /// <summary>
        /// 从 YAML 字符串加载分数表。
        /// </summary>
        public static LoadResult LoadFromString(string yamlContent)
        {
            var scoreTable = new Dictionary<string, int>();
            var current = new Dictionary<string, string>();
            bool inEnemiesSection = false;

            foreach (string line in yamlContent.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                // Detect top-level enemies: key
                if (trimmed == "enemies:")
                {
                    inEnemiesSection = true;
                    continue;
                }

                if (!inEnemiesSection)
                    continue;

                // New enemy entry starts with "- enemy_id:"
                if (trimmed.StartsWith("- enemy_id:"))
                {
                    // Save previous entry
                    if (current.Count > 0)
                        SaveEnemyEntry(current, scoreTable);

                    current = new Dictionary<string, string>();
                    current["enemy_id"] = YamlKeyValueParser.ParseValue(trimmed, "- enemy_id:");
                }
                else if (trimmed.StartsWith("enemy_id:"))
                {
                    current["enemy_id"] = YamlKeyValueParser.ParseValue(trimmed, "enemy_id:");
                }
                else if (trimmed.StartsWith("score:"))
                {
                    current["score"] = YamlKeyValueParser.ParseValue(trimmed, "score:");
                }
            }

            // Last entry
            if (current.Count > 0)
                SaveEnemyEntry(current, scoreTable);

            return new LoadResult(scoreTable);
        }

        private static void SaveEnemyEntry(Dictionary<string, string> fields, Dictionary<string, int> scoreTable)
        {
            if (!fields.TryGetValue("enemy_id", out var id) || string.IsNullOrEmpty(id))
                return;

            int score = DefaultScore;
            if (fields.TryGetValue("score", out var scoreStr) && int.TryParse(scoreStr, out int parsed))
                score = parsed;

            scoreTable[id] = score;
        }
    }
}
