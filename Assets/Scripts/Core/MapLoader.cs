// T-SYS-007 (map_loading/Core) — 地图 YAML 加载器 + 引用校验。
// 设计来源: templates/system_design/map_loading.md（Core 纯逻辑层，零 UnityEngine 依赖）。
// 镜像规则: 与 Assets/Scripts/Core/MapLoader.cs 内容一一对应。

using System;
using System.Collections.Generic;
using System.IO;

namespace Contra3D.Core
{
    /// <summary>
    /// 从 YAML 文件加载并校验地图定义。使用简单文本解析（不引入新依赖）。
    /// 校验规则:
    ///   - SpawnPoint 数量 ≥ 2
    ///   - SpawnPoint 间距 ≥ 5 m
    ///   - CoverPoint 数量 ≥ SpawnPoint 数量的 50%
    ///   - PickupLocation 数量 ≤ 20
    ///   - 所有坐标 X 绝对值 ≤ collision_bound_x（默认 25.0）
    /// </summary>
    public static class MapLoader
    {
        /// <summary>默认碰撞边界 X 半宽（米）。</summary>
        public const float DefaultCollisionBoundX = 25.0f;

        /// <summary>SpawnPoint 最小间距（米）。</summary>
        private const float MinSpawnDistance = 5.0f;

        /// <summary>
        /// 从 YAML 文件路径加载地图定义。
        /// </summary>
        /// <returns>校验通过返回 <see cref="MapDefinition"/>；否则抛出 <see cref="MapLoadException"/>。</returns>
        /// <exception cref="MapLoadException">校验失败时抛出，携带全部 <see cref="MapValidationError"/>。</exception>
        public static MapDefinition Load(string yamlPath)
        {
            if (!File.Exists(yamlPath))
                throw new FileNotFoundException($"Map YAML not found: {yamlPath}");

            string content = File.ReadAllText(yamlPath);
            return LoadFromString(content);
        }

        /// <summary>
        /// 从 YAML 字符串加载地图定义。
        /// </summary>
        public static MapDefinition LoadFromString(string yamlContent)
        {
            var maps = ParseMaps(yamlContent);
            if (maps.Count == 0)
                throw new MapLoadException(new List<MapValidationError>
                {
                    new MapValidationError("maps", "No map entries found in YAML.")
                });

            // 取第一张地图作为主地图加载（seed 数据通常仅一张）
            var first = maps[0];
            // Apply default collision bound if not explicitly set
            if (first.CollisionBoundX <= 0f)
                first.CollisionBoundX = DefaultCollisionBoundX;
            var errors = ValidateMap(first);
            if (errors.Count > 0)
                throw new MapLoadException(errors);

            return new MapDefinition(
                first.MapId,
                first.Name,
                first.SpawnPoints.ToArray(),
                first.CoverPoints.ToArray(),
                first.PickupLocations.ToArray(),
                first.CollisionBoundX);
        }

        #region Parsing

        private static List<ParsedMap> ParseMaps(string yamlContent)
        {
            var result = new List<ParsedMap>();
            string[] lines = yamlContent.Split('\n');

            bool inMaps = false;
            bool inMapBlock = false;
            bool inSpawn = false;
            bool inCover = false;
            bool inPickup = false;
            ParsedMap current = null;
            var currentSpawn = new Dictionary<string, string>();
            var currentCover = new Dictionary<string, string>();
            var currentPickup = new Dictionary<string, string>();

            void FlushCurrentSpawn()
            {
                if (current != null && currentSpawn.Count > 0)
                {
                    current.SpawnPoints.Add(ParseSpawnPoint(currentSpawn));
                    currentSpawn.Clear();
                }
            }

            void FlushCurrentCover()
            {
                if (current != null && currentCover.Count > 0)
                {
                    current.CoverPoints.Add(ParseCoverPoint(currentCover));
                    currentCover.Clear();
                }
            }

            void FlushCurrentPickup()
            {
                if (current != null && currentPickup.Count > 0)
                {
                    current.PickupLocations.Add(ParsePickupLocation(currentPickup));
                    currentPickup.Clear();
                }
            }

            void StartNewEntry(string section)
            {
                // Flush previous entry before starting a new one
                if (section == "spawn")
                {
                    FlushCurrentSpawn();
                }
                else if (section == "cover")
                {
                    FlushCurrentCover();
                }
                else if (section == "pickup")
                {
                    FlushCurrentPickup();
                }
            }

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Top-level maps: list
                if (line == "maps:")
                {
                    inMaps = true;
                    continue;
                }

                if (!inMaps)
                    continue;

                // New map block
                if (line.StartsWith("- map_id:"))
                {
                    FlushCurrentSpawn();
                    FlushCurrentCover();
                    FlushCurrentPickup();
                    current = new ParsedMap();
                    string id = line.Substring("- map_id:".Length).Trim().Trim('"').Trim('\'');
                    current.MapId = id;
                    inMapBlock = true;
                    inSpawn = false;
                    inCover = false;
                    inPickup = false;
                    continue;
                }

                if (!inMapBlock || current == null)
                    continue;

                // Section headers
                if (line == "spawn_points:")
                {
                    inSpawn = true;
                    inCover = false;
                    inPickup = false;
                    continue;
                }
                if (line == "cover_points:")
                {
                    FlushCurrentSpawn();
                    inCover = true;
                    inSpawn = false;
                    inPickup = false;
                    continue;
                }
                if (line == "pickup_locations:")
                {
                    FlushCurrentSpawn();
                    FlushCurrentCover();
                    inPickup = true;
                    inSpawn = false;
                    inCover = false;
                    continue;
                }

                // Inline map-level keys
                if (line.StartsWith("name:"))
                {
                    current.Name = line.Substring("name:".Length).Trim().Trim('"').Trim('\'');
                    continue;
                }
                if (line.StartsWith("collision_bound_x:"))
                {
                    string val = line.Substring("collision_bound_x:".Length).Trim();
                    float.TryParse(val, out float cbx);
                    current.CollisionBoundX = cbx;
                    continue;
                }

                // New entry marker (list item) — flush previous before starting new
                bool isListMarker = line.StartsWith("- ") || line == "-";

                // Inline object: {x: 0, y: 0, z: 0, team: player}
                if (line.StartsWith("- {"))
                {
                    if (inSpawn)
                    {
                        StartNewEntry("spawn");
                        ParseInlineObj(line, currentSpawn);
                        continue;
                    }
                    if (inCover)
                    {
                        StartNewEntry("cover");
                        ParseInlineObj(line, currentCover);
                        continue;
                    }
                    if (inPickup)
                    {
                        StartNewEntry("pickup");
                        ParseInlineObj(line, currentPickup);
                        continue;
                    }
                }

                // Multi-line entries: "- x:" followed by "  y:" / "  z:"
                if (line.StartsWith("- x:"))
                {
                    if (inSpawn)
                    {
                        StartNewEntry("spawn");
                        currentSpawn["x"] = ParseVal(line.Substring("- x:".Length));
                    }
                    else if (inCover)
                    {
                        StartNewEntry("cover");
                        currentCover["x"] = ParseVal(line.Substring("- x:".Length));
                    }
                    else if (inPickup)
                    {
                        StartNewEntry("pickup");
                        currentPickup["x"] = ParseVal(line.Substring("- x:".Length));
                    }
                    continue;
                }

                // Continuation lines (indented keys like "  y:" or "  z:" or "  team:")
                if (isListMarker && line.Length > 2)
                {
                    // Skip bare "-" markers
                    continue;
                }

                // Indented keys under a multi-line entry
                if (line.StartsWith("x:") || line.StartsWith("  x:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim();
                    if (inSpawn) currentSpawn["x"] = val;
                    else if (inCover) currentCover["x"] = val;
                    else if (inPickup) currentPickup["x"] = val;
                    continue;
                }
                if (line.StartsWith("y:") || line.StartsWith("  y:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim();
                    if (inSpawn) currentSpawn["y"] = val;
                    else if (inCover) currentCover["y"] = val;
                    else if (inPickup) currentPickup["y"] = val;
                    continue;
                }
                if (line.StartsWith("z:") || line.StartsWith("  z:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim();
                    if (inSpawn) currentSpawn["z"] = val;
                    else if (inCover) currentCover["z"] = val;
                    else if (inPickup) currentPickup["z"] = val;
                    continue;
                }
                if (line.StartsWith("team:") || line.StartsWith("  team:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                    if (inSpawn) currentSpawn["team"] = val;
                    continue;
                }
                if (line.StartsWith("type:") || line.StartsWith("  type:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                    if (inPickup) currentPickup["type"] = val;
                    continue;
                }
            }

            FlushCurrentSpawn();
            FlushCurrentCover();
            FlushCurrentPickup();
            if (current != null)
                result.Add(current);

            return result;
        }

        private static void ParseInlineObj(string line, Dictionary<string, string> dict)
        {
            // Remove leading "- {" and trailing "}"
            string inner = line.Substring(2).Trim(); // strip "- "
            if (inner.StartsWith("{"))
                inner = inner.Substring(1);
            if (inner.EndsWith("}"))
                inner = inner.Substring(0, inner.Length - 1);
            inner = inner.Trim();

            string[] parts = inner.Split(',');
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

        private static string ParseVal(string raw)
        {
            return raw.Trim().Trim('"').Trim('\'');
        }

        private static SpawnPoint ParseSpawnPoint(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            string teamStr = f.TryGetValue("team", out var t) ? t.ToLower() : "player";
            SpawnTeam team = teamStr == "enemy" ? SpawnTeam.Enemy : SpawnTeam.Player;
            return new SpawnPoint(x, y, z, team);
        }

        private static CoverPoint ParseCoverPoint(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            return new CoverPoint(x, y, z);
        }

        private static PickupLocation ParsePickupLocation(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            string typeStr = f.TryGetValue("type", out var t) ? t.ToLower() : "weapon";
            PickupType type = typeStr switch
            {
                "health" => PickupType.Health,
                "ammo" => PickupType.Ammo,
                _ => PickupType.Weapon
            };
            return new PickupLocation(x, y, z, type);
        }

        private static float ParseFloat(Dictionary<string, string> f, string key, float fallback)
        {
            if (!f.TryGetValue(key, out var s) || string.IsNullOrEmpty(s))
                return fallback;
            float.TryParse(s, out float v);
            return v;
        }

        #endregion

        #region Validation

        private static List<MapValidationError> ValidateMap(ParsedMap m)
        {
            var errors = new List<MapValidationError>();
            float bound = m.CollisionBoundX > 0f ? m.CollisionBoundX : DefaultCollisionBoundX;

            if (m.SpawnPoints.Count < 2)
                errors.Add(new MapValidationError("spawn_points",
                    $"Need at least 2 spawn points, got {m.SpawnPoints.Count}."));

            for (int i = 0; i < m.SpawnPoints.Count; i++)
            {
                SpawnPoint sp = m.SpawnPoints[i];
                if (Math.Abs(sp.X) > bound)
                    errors.Add(new MapValidationError($"spawn_points[{i}].x",
                        $"Coordinate {sp.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(sp.Z) > bound)
                    errors.Add(new MapValidationError($"spawn_points[{i}].z",
                        $"Coordinate {sp.Z} is outside collision boundary ±{bound}."));

                for (int j = i + 1; j < m.SpawnPoints.Count; j++)
                {
                    float dist = sp.DistanceTo(m.SpawnPoints[j]);
                    if (dist < MinSpawnDistance)
                        errors.Add(new MapValidationError($"spawn_points[{i}]↔spawn_points[{j}]",
                            $"Distance {dist:F2}m is below minimum {MinSpawnDistance}m."));
                }
            }

            int minCoverCount = (m.SpawnPoints.Count + 1) / 2; // ceil(spawnCount / 2)
            if (m.CoverPoints.Count < minCoverCount)
                errors.Add(new MapValidationError("cover_points",
                    $"Need at least {minCoverCount} cover points (≥50% of {m.SpawnPoints.Count} spawns), got {m.CoverPoints.Count}."));

            for (int i = 0; i < m.CoverPoints.Count; i++)
            {
                CoverPoint cp = m.CoverPoints[i];
                if (Math.Abs(cp.X) > bound)
                    errors.Add(new MapValidationError($"cover_points[{i}].x",
                        $"Coordinate {cp.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(cp.Z) > bound)
                    errors.Add(new MapValidationError($"cover_points[{i}].z",
                        $"Coordinate {cp.Z} is outside collision boundary ±{bound}."));
            }

            if (m.PickupLocations.Count > 20)
                errors.Add(new MapValidationError("pickup_locations",
                    $"Too many pickups: {m.PickupLocations.Count} (max 20)."));

            for (int i = 0; i < m.PickupLocations.Count; i++)
            {
                PickupLocation pl = m.PickupLocations[i];
                if (Math.Abs(pl.X) > bound)
                    errors.Add(new MapValidationError($"pickup_locations[{i}].x",
                        $"Coordinate {pl.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(pl.Z) > bound)
                    errors.Add(new MapValidationError($"pickup_locations[{i}].z",
                        $"Coordinate {pl.Z} is outside collision boundary ±{bound}."));
            }

            return errors;
        }

        #endregion

        #region Helpers

        private class ParsedMap
        {
            public string MapId;
            public string Name;
            public float CollisionBoundX;
            public readonly List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();
            public readonly List<CoverPoint> CoverPoints = new List<CoverPoint>();
            public readonly List<PickupLocation> PickupLocations = new List<PickupLocation>();
        }

        /// <summary>地图加载/校验异常，携带全部验证错误。</summary>
        public sealed class MapLoadException : Exception
        {
            public List<MapValidationError> Errors { get; }

            public MapLoadException(List<MapValidationError> errors)
                : base(BuildMessage(errors))
            {
                Errors = errors ?? throw new ArgumentException("Errors must not be null.", nameof(errors));
            }

            private static string BuildMessage(List<MapValidationError> errors)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Map validation failed with the following errors:");
                foreach (var e in errors)
                    sb.AppendLine($"  - {e}");
                return sb.ToString();
            }
        }

        #endregion
    }
}
