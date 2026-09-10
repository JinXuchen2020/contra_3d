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
            bool inPatrolPath = false;
            bool inWaypoints = false;
            ParsedMap current = null;
            var currentSpawn = new Dictionary<string, string>();
            var currentCover = new Dictionary<string, string>();
            var currentPickup = new Dictionary<string, string>();
            var currentWaypoint = new Dictionary<string, string>();
            string currentPathId = null;

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

            void FlushCurrentWaypoint()
            {
                if (current != null && inWaypoints && currentWaypoint.Count > 0 && currentPathId != null)
                {
                    var wp = ParsePatrolWaypoint(currentWaypoint);
                    // Find or create the current path
                    PatrolPath path = default;
                    bool found = false;
                    foreach (var p in current.PatrolPaths)
                    {
                        if (p.PathId == currentPathId) { path = p; found = true; break; }
                    }
                    if (!found)
                    {
                        var waypoints = new List<PatrolWaypoint> { wp };
                        current.PatrolPaths.Add(new PatrolPath(currentPathId, waypoints.ToArray()));
                    }
                    else
                    {
                        var updated = new List<PatrolWaypoint>(path.Waypoints) { wp };
                        // Remove old and add new with updated waypoints
                        current.PatrolPaths.RemoveAt(current.PatrolPaths.IndexOf(path));
                        current.PatrolPaths.Add(new PatrolPath(currentPathId, updated.ToArray()));
                    }
                    currentWaypoint.Clear();
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
                if (line == "patrol_paths:")
                {
                    FlushCurrentSpawn();
                    FlushCurrentCover();
                    FlushCurrentPickup();
                    inPatrolPath = true;
                    inWaypoints = false;
                    inSpawn = false;
                    inCover = false;
                    inPickup = false;
                    currentPathId = null;
                    continue;
                }

                // patrol_paths: path_id marker
                if (inPatrolPath && line.StartsWith("- path_id:"))
                {
                    FlushCurrentWaypoint();
                    currentPathId = YamlKeyValueParser.ParseValue(line, "- path_id:");
                    inWaypoints = false;
                    continue;
                }

                // waypoints subsection
                if (inPatrolPath && line.StartsWith("waypoints:"))
                {
                    inWaypoints = true;
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
                    if (inPatrolPath && inWaypoints)
                    {
                        FlushCurrentWaypoint();
                        ParseInlineObj(line, currentWaypoint);
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
                    else if (inSpawn) currentSpawn["type"] = val;
                    continue;
                }
                if (line.StartsWith("trigger:") || line.StartsWith("  trigger:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    if (inSpawn) currentSpawn["trigger"] = val;
                    continue;
                }
                if (line.StartsWith("facing_normal:") || line.StartsWith("  facing_normal:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    if (inCover) currentCover["facing_normal"] = val;
                    continue;
                }
                if (line.StartsWith("spawn_id:") || line.StartsWith("  spawn_id:"))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    if (inPickup) currentPickup["spawn_id"] = val;
                    continue;
                }
                // Patrol waypoint continuation keys
                if (inPatrolPath && inWaypoints && (line.StartsWith("wait_s:") || line.StartsWith("  wait_s:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim();
                    currentWaypoint["wait_s"] = val;
                    continue;
                }
                if (inPatrolPath && inWaypoints && (line.StartsWith("speed:") || line.StartsWith("  speed:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim();
                    currentWaypoint["speed"] = val;
                    continue;
                }
            }

            FlushCurrentSpawn();
            FlushCurrentCover();
            FlushCurrentPickup();
            FlushCurrentWaypoint();
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

        private static SpawnPoint ParseSpawnPoint(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            string teamStr = f.TryGetValue("team", out var t) ? t.ToLower() : "player";
            SpawnTeam team = teamStr == "enemy" ? SpawnTeam.Enemy : SpawnTeam.Player;
            string typeStr = f.TryGetValue("type", out var typ) ? typ.ToLower() : "patrol";
            SpawnType type = typeStr switch
            {
                "ambush" => SpawnType.Ambush,
                "trigger" => SpawnType.Trigger,
                "reinforce" => SpawnType.Reinforce,
                _ => SpawnType.Patrol
            };
            f.TryGetValue("trigger", out var trigger);
            return new SpawnPoint(x, y, z, team, type, trigger);
        }

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

        private static float ParseFloat(Dictionary<string, string> f, string key, float fallback)
        {
            if (!f.TryGetValue(key, out var s) || string.IsNullOrEmpty(s))
                return fallback;
            float.TryParse(s, out float v);
            return v;
        }

        private static PatrolWaypoint ParsePatrolWaypoint(Dictionary<string, string> f)
        {
            float x = ParseFloat(f, "x", 0f);
            float y = ParseFloat(f, "y", 0f);
            float z = ParseFloat(f, "z", 0f);
            float waitSeconds = ParseFloat(f, "wait_s", 0f);
            float speed = ParseFloat(f, "speed", 1f);
            return new PatrolWaypoint(x, y, z, waitSeconds, speed);
        }

        #endregion
    }
}
