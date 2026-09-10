// T-SYS-007 (map_loading/Core) — MapLoader entry continuation-line parsing helpers (partial class)。
// 与 MapLoader.Parsers.cs 配合使用，通过 partial class 共享同一命名空间。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Continuation Line Parsing

        /// <summary>处理多行条目的续行（缩进键值）。</summary>
        private static bool TryParseContinuationLine(string line, bool inSpawn, bool inCover, bool inPickup,
            bool inPatrolPath, bool inWaypoints, bool inEncounterZone,
            Dictionary<string, string> currentSpawn, Dictionary<string, string> currentCover,
            Dictionary<string, string> currentPickup, Dictionary<string, string> currentWaypoint,
            Dictionary<string, string> currentZone)
        {
            // Patrol waypoint continuation keys
            if (inPatrolPath && inWaypoints)
            {
                if (line.StartsWith("wait_s:") || line.StartsWith("  wait_s:"))
                {
                    currentWaypoint["wait_s"] = line.Substring(line.IndexOf(':') + 1).Trim();
                    return true;
                }
                if (line.StartsWith("speed:") || line.StartsWith("  speed:"))
                {
                    currentWaypoint["speed"] = line.Substring(line.IndexOf(':') + 1).Trim();
                    return true;
                }
            }

            // Encounter zone keys
            if (inEncounterZone)
            {
                if (line.StartsWith("zone_id:") || line.StartsWith("  zone_id:"))
                {
                    currentZone["zone_id"] = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    return true;
                }
                if (line.StartsWith("bounds:") || line.StartsWith("  bounds:"))
                {
                    currentZone["bounds"] = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    return true;
                }
                if (line.StartsWith("on_enter:") || line.StartsWith("  on_enter:"))
                {
                    currentZone["on_enter"] = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                    return true;
                }
                if (line.StartsWith("lock:") || line.StartsWith("  lock:"))
                {
                    currentZone["lock"] = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                    return true;
                }
            }

            // Shared coordinate keys
            if (line.StartsWith("x:") || line.StartsWith("  x:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim();
                if (inSpawn) currentSpawn["x"] = val;
                else if (inCover) currentCover["x"] = val;
                else if (inPickup) currentPickup["x"] = val;
                return true;
            }
            if (line.StartsWith("y:") || line.StartsWith("  y:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim();
                if (inSpawn) currentSpawn["y"] = val;
                else if (inCover) currentCover["y"] = val;
                else if (inPickup) currentPickup["y"] = val;
                return true;
            }
            if (line.StartsWith("z:") || line.StartsWith("  z:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim();
                if (inSpawn) currentSpawn["z"] = val;
                else if (inCover) currentCover["z"] = val;
                else if (inPickup) currentPickup["z"] = val;
                return true;
            }

            // Team / type / trigger (spawn only)
            if (line.StartsWith("team:") || line.StartsWith("  team:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                if (inSpawn) currentSpawn["team"] = val;
                return true;
            }
            if (line.StartsWith("type:") || line.StartsWith("  type:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim().ToLower();
                if (inPickup) currentPickup["type"] = val;
                else if (inSpawn) currentSpawn["type"] = val;
                return true;
            }
            if (line.StartsWith("trigger:") || line.StartsWith("  trigger:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                if (inSpawn) currentSpawn["trigger"] = val;
                return true;
            }

            // Cover-only keys
            if (line.StartsWith("facing_normal:") || line.StartsWith("  facing_normal:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                if (inCover) currentCover["facing_normal"] = val;
                return true;
            }

            // Pickup-only keys
            if (line.StartsWith("spawn_id:") || line.StartsWith("  spawn_id:"))
            {
                string val = line.Substring(line.IndexOf(':') + 1).Trim().Trim('"').Trim('\'');
                if (inPickup) currentPickup["spawn_id"] = val;
                return true;
            }

            return false;
        }

        #endregion
    }
}
