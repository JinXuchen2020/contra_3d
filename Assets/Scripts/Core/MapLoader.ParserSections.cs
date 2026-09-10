// T-SYS-007 (map_loading/Core) — MapLoader section-header / entry-dispatch helpers (partial class)。
// 与 MapLoader.Parsers.cs 配合使用，通过 partial class 共享同一命名空间。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Section Header Handling

        /// <summary>根据行内容更新section状态标志并返回是否为新section。</summary>
        private static bool TryAdvanceSection(string line, bool inMaps,
            ref bool inMapBlock, ref bool inSpawn, ref bool inCover,
            ref bool inPickup, ref bool inPatrolPath, ref bool inWaypoints,
            ref bool inEncounterZone)
        {
            if (!inMaps) return false;
            if (!inMapBlock) return false;

            if (line == "spawn_points:")
            {
                inSpawn = true; inCover = false; inPickup = false;
                inPatrolPath = false; inWaypoints = false; inEncounterZone = false;
                return true;
            }
            if (line == "cover_points:")
            {
                inCover = true; inSpawn = false; inPickup = false;
                inPatrolPath = false; inWaypoints = false; inEncounterZone = false;
                return true;
            }
            if (line == "pickup_locations:")
            {
                inPickup = true; inSpawn = false; inCover = false;
                inPatrolPath = false; inWaypoints = false; inEncounterZone = false;
                return true;
            }
            if (line == "patrol_paths:")
            {
                inPatrolPath = true; inWaypoints = false;
                inSpawn = false; inCover = false; inPickup = false; inEncounterZone = false;
                return true;
            }
            if (line == "encounter_zones:")
            {
                inEncounterZone = true;
                inSpawn = false; inCover = false; inPickup = false;
                inPatrolPath = false; inWaypoints = false;
                return true;
            }
            return false;
        }

        /// <summary>处理 patrol path 和 waypoint 子节标记。</summary>
        private static bool TryHandlePatrolSubsection(string line, bool inPatrolPath, ref bool inWaypoints,
            ref string currentPathId)
        {
            if (!inPatrolPath) return false;
            if (line.StartsWith("- path_id:"))
            {
                currentPathId = YamlKeyValueParser.ParseValue(line, "- path_id:");
                inWaypoints = false;
                return true;
            }
            if (line.StartsWith("waypoints:"))
            {
                inWaypoints = true;
                return true;
            }
            return false;
        }

        #endregion

        #region Entry Dispatch

        /// <summary>根据当前section和行内容分派到相应的条目解析逻辑。</summary>
        /// <returns>true if the line was handled; false otherwise.</returns>
        private static bool TryDispatchEntry(string line, bool inSpawn, bool inCover, bool inPickup,
            bool inPatrolPath, bool inWaypoints, bool inEncounterZone,
            ParsedMap current, Dictionary<string, string> currentSpawn,
            Dictionary<string, string> currentCover, Dictionary<string, string> currentPickup,
            Dictionary<string, string> currentWaypoint, Dictionary<string, string> currentZone,
            ref string currentPathId)
        {
            bool isInline = line.StartsWith("- {");
            bool isMultilineX = line.StartsWith("- x:");

            if (isInline)
            {
                if (inSpawn)
                {
                    StartNewEntry("spawn", current, currentSpawn, currentCover, currentPickup);
                    ParseInlineObj(line, currentSpawn);
                    return true;
                }
                if (inCover)
                {
                    StartNewEntry("cover", current, currentSpawn, currentCover, currentPickup);
                    ParseInlineObj(line, currentCover);
                    return true;
                }
                if (inPickup)
                {
                    StartNewEntry("pickup", current, currentSpawn, currentCover, currentPickup);
                    ParseInlineObj(line, currentPickup);
                    return true;
                }
                if (inPatrolPath && inWaypoints)
                {
                    FlushCurrentWaypoint(current, currentWaypoint, ref currentPathId);
                    ParseInlineObj(line, currentWaypoint);
                    return true;
                }
                if (inEncounterZone)
                {
                    FlushCurrentEncounterZone(current, currentZone);
                    ParseInlineObj(line, currentZone);
                    return true;
                }
            }

            if (isMultilineX)
            {
                if (inSpawn)
                {
                    StartNewEntry("spawn", current, currentSpawn, currentCover, currentPickup);
                    currentSpawn["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
                    return true;
                }
                if (inCover)
                {
                    StartNewEntry("cover", current, currentSpawn, currentCover, currentPickup);
                    currentCover["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
                    return true;
                }
                if (inPickup)
                {
                    StartNewEntry("pickup", current, currentSpawn, currentCover, currentPickup);
                    currentPickup["x"] = YamlKeyValueParser.ParseValue(line, "- x:");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Flush Helpers

        /// <summary>刷新当前 spawn point 条目。</summary>
        private static void FlushCurrentSpawn(ParsedMap current, Dictionary<string, string> currentSpawn)
        {
            if (current != null && currentSpawn.Count > 0)
            {
                current.SpawnPoints.Add(ParseSpawnPoint(currentSpawn));
                currentSpawn.Clear();
            }
        }

        /// <summary>刷新当前 cover point 条目。</summary>
        private static void FlushCurrentCover(ParsedMap current, Dictionary<string, string> currentCover)
        {
            if (current != null && currentCover.Count > 0)
            {
                current.CoverPoints.Add(ParseCoverPoint(currentCover));
                currentCover.Clear();
            }
        }

        /// <summary>刷新当前 pickup location 条目。</summary>
        private static void FlushCurrentPickup(ParsedMap current, Dictionary<string, string> currentPickup)
        {
            if (current != null && currentPickup.Count > 0)
            {
                current.PickupLocations.Add(ParsePickupLocation(currentPickup));
                currentPickup.Clear();
            }
        }

        /// <summary>刷新当前 waypoint 条目并追加到 PatrolPaths。</summary>
        private static void FlushCurrentWaypoint(ParsedMap current, Dictionary<string, string> currentWaypoint,
            ref string currentPathId)
        {
            if (current == null || !currentWaypoint.ContainsKey("x") || currentPathId == null) return;
            var wp = ParsePatrolWaypoint(currentWaypoint);
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
                current.PatrolPaths.RemoveAt(current.PatrolPaths.IndexOf(path));
                current.PatrolPaths.Add(new PatrolPath(currentPathId, updated.ToArray()));
            }
            currentWaypoint.Clear();
        }

        /// <summary>刷新当前 encounter zone 条目。</summary>
        private static void FlushCurrentEncounterZone(ParsedMap current, Dictionary<string, string> currentZone)
        {
            if (current != null && currentZone.Count > 0)
            {
                current.EncounterZones.Add(ParseEncounterZone(currentZone));
                currentZone.Clear();
            }
        }

        /// <summary>开始新条目前刷新前一个同类型条目。</summary>
        private static void StartNewEntry(string section, ParsedMap current,
            Dictionary<string, string> currentSpawn, Dictionary<string, string> currentCover,
            Dictionary<string, string> currentPickup)
        {
            if (section == "spawn" && current != null && currentSpawn.Count > 0)
            {
                current.SpawnPoints.Add(ParseSpawnPoint(currentSpawn));
                currentSpawn.Clear();
            }
            else if (section == "cover" && current != null && currentCover.Count > 0)
            {
                current.CoverPoints.Add(ParseCoverPoint(currentCover));
                currentCover.Clear();
            }
            else if (section == "pickup" && current != null && currentPickup.Count > 0)
            {
                current.PickupLocations.Add(ParsePickupLocation(currentPickup));
                currentPickup.Clear();
            }
        }

        #endregion
    }
}
