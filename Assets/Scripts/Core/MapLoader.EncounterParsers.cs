// T-SYS-007 (map_loading/Core) — Encounter zone parsing logic (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Encounter Parsers

        private static EncounterZone ParseEncounterZone(Dictionary<string, string> f)
        {
            string zoneId = f.TryGetValue("zone_id", out var zid) ? zid.Trim() : null;
            string boundsStr = f.TryGetValue("bounds", out var bnd) ? bnd.Trim() : null;
            string onEnter = f.TryGetValue("on_enter", out var oe) ? oe.Trim() : "";
            bool lockRetreat = false;
            if (f.TryGetValue("lock", out var lk))
            {
                string lv = lk.Trim().ToLower();
                lockRetreat = lv == "true" || lv == "1" || lv == "yes";
            }

            float xMin = 0f, xMax = 0f, zMin = 0f, zMax = 0f;
            if (!string.IsNullOrWhiteSpace(boundsStr))
            {
                var parts = ParseVector3Components(boundsStr);
                // bounds may be [xMin, xMax, zMin, zMax] (4 values) or [xMin, zMin, xMax, zMax]
                // For simplicity, support 4-value AABB: [xMin, xMax, zMin, zMax]
                float[] b = new float[4];
                string[] bp = boundsStr.Trim('[', ']').Split(',');
                for (int i = 0; i < 4 && i < bp.Length; i++)
                    float.TryParse(bp[i].Trim(), out b[i]);
                xMin = b[0]; xMax = b[1]; zMin = b[2]; zMax = b[3];
            }

            return new EncounterZone(zoneId, xMin, xMax, zMin, zMax, onEnter, lockRetreat);
        }

        #endregion
    }
}
