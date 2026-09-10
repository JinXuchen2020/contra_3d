// T-SYS-007 (map_loading/Core) — Patrol path parsing logic (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Patrol Parsers

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
