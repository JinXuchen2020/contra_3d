// T-SYS-007 (map_loading/Core) — MapLoader 主解析逻辑 (partial class)。
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
            bool inEncounterZone = false;
            ParsedMap current = null;
            var currentSpawn = new Dictionary<string, string>();
            var currentCover = new Dictionary<string, string>();
            var currentPickup = new Dictionary<string, string>();
            var currentWaypoint = new Dictionary<string, string>();
            var currentZone = new Dictionary<string, string>();
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

            void FlushCurrentEncounterZone()
            {
                if (current != null && currentZone.Count > 0)
                {
                    current.EncounterZones.Add(ParseEncounterZone(currentZone));
                    currentZone.Clear();
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
                    inEncounterZone = false;
                    currentPathId = null;
                    continue;
                }
                if (line == "encounter_zones:")
                {
                    FlushCurrentSpawn();
                    FlushCurrentCover();
                    FlushCurrentPickup();
                    FlushCurrentWaypoint();
                    inEncounterZone = true;
                    inSpawn = false;
                    inCover = false;
                    inPickup = false;
                    inPatrolPath = false;
                    inWaypoints = false;
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
                    if (inEncounterZone)
                    {
                        FlushCurrentEncounterZone();
                        ParseInlineObj(line, currentZone);
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
                // Encounter zone keys
                if (inEncounterZone && (line.StartsWith("zone_id:") || line.StartsWith("  zone_id:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    currentZone["zone_id"] = val;
                    continue;
                }
                if (inEncounterZone && (line.StartsWith("bounds:") || line.StartsWith("  bounds:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    currentZone["bounds"] = val;
                    continue;
                }
                if (inEncounterZone && (line.StartsWith("on_enter:") || line.StartsWith("  on_enter:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    currentZone["on_enter"] = val;
                    continue;
                }
                if (inEncounterZone && (line.StartsWith("lock:") || line.StartsWith("  lock:")))
                {
                    string val = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                    currentZone["lock"] = val;
                    continue;
                }
            }

            FlushCurrentSpawn();
            FlushCurrentCover();
            FlushCurrentPickup();
            FlushCurrentWaypoint();
            FlushCurrentEncounterZone();
            if (current != null)
                result.Add(current);

            return result;
        }

        #endregion
    }
}
