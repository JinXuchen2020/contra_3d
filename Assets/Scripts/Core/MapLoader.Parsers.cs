// T-SYS-007 (map_loading/Core) — MapLoader 解析逻辑 (partial class)。
// 设计来源: templates/system_design/map_loading.md。
// 与 MapLoader.cs 配合使用，通过 partial class 共享同一命名空间。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Parsing

        private static List<ParsedMap> ParseMaps(string yamlContent)
        {
            var result = new List<ParsedMap>();
            // LOAD-TIME ONLY - not in hot path
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
                    string id = YamlKeyValueParser.ParseValue(line, "- map_id:");
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
                    current.Name = YamlKeyValueParser.ParseValue(line, "name:");
                    continue;
                }
                if (line.StartsWith("collision_bound_x:"))
                {
                    string val = YamlKeyValueParser.ParseValue(line, "collision_bound_x:");
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
                        currentSpawn["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
                    }
                    else if (inCover)
                    {
                        StartNewEntry("cover");
                        currentCover["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
                    }
                    else if (inPickup)
                    {
                        StartNewEntry("pickup");
                        currentPickup["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
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
                if (line.StartsWith("spawn_id:") || line.StartsWith("  spawn_id:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    if (inPickup) currentPickup["spawn_id"] = val;
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
            f.TryGetValue("spawn_id", out var spawnId);
            return new PickupLocation(x, y, z, type, spawnId);
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
