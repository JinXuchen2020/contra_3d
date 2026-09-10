// T-SYS-007 (map_loading/Core) — MapLoader 主解析逻辑 (partial class)。
// 设计来源: templates/system_design/map_loading.md。
// 与 MapLoader.cs / MapLoader.ParserSections.cs / MapLoader.ParserEntries.cs 配合使用，
// 通过 partial class 共享同一命名空间。

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
                    // Flush all pending entries before starting a new map
                    FlushCurrentSpawn(current, currentSpawn);
                    FlushCurrentCover(current, currentCover);
                    FlushCurrentPickup(current, currentPickup);
                    FlushCurrentWaypoint(current, currentWaypoint, ref currentPathId);
                    FlushCurrentEncounterZone(current, currentZone);

                    current = new ParsedMap();
                    string id = YamlKeyValueParser.ParseValue(line, "- map_id:");
                    current.MapId = id;
                    inMapBlock = true;
                    inSpawn = false;
                    inCover = false;
                    inPickup = false;
                    inPatrolPath = false;
                    inWaypoints = false;
                    inEncounterZone = false;
                    currentPathId = null;
                    continue;
                }

                if (!inMapBlock || current == null)
                    continue;

                // Section headers → delegate to helper
                if (TryAdvanceSection(line, inMaps, ref inMapBlock, ref inSpawn, ref inCover,
                    ref inPickup, ref inPatrolPath, ref inWaypoints, ref inEncounterZone))
                    continue;

                // Patrol sub-section markers → delegate to helper
                if (TryHandlePatrolSubsection(line, inPatrolPath, ref inWaypoints, ref currentPathId))
                    continue;

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

                // Entry dispatch (inline / multi-line start) → delegate to helper
                if (TryDispatchEntry(line, inSpawn, inCover, inPickup, inPatrolPath, inWaypoints,
                    inEncounterZone, current, currentSpawn, currentCover, currentPickup,
                    currentWaypoint, currentZone, ref currentPathId))
                    continue;

                // Skip bare "-" list markers
                bool isListMarker = line.StartsWith("- ") || line == "-";
                if (isListMarker && line.Length > 2)
                    continue;

                // Continuation lines → delegate to helper
                if (TryParseContinuationLine(line, inSpawn, inCover, inPickup,
                    inPatrolPath, inWaypoints, inEncounterZone,
                    currentSpawn, currentCover, currentPickup, currentWaypoint, currentZone))
                    continue;
            }

            // Final flush for last map
            FlushCurrentSpawn(current, currentSpawn);
            FlushCurrentCover(current, currentCover);
            FlushCurrentPickup(current, currentPickup);
            FlushCurrentWaypoint(current, currentWaypoint, ref currentPathId);
            FlushCurrentEncounterZone(current, currentZone);
            if (current != null)
                result.Add(current);

            return result;
        }

        #endregion
    }
}
