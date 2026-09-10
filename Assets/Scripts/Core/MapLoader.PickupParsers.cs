// T-SYS-007 (map_loading/Core) — Pickup location parsing logic (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Pickup Parsers

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

        #endregion
    }
}
