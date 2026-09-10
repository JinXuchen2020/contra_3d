// T-SYS-007 (map_loading/Core) — Spawn point parsing logic (partial class)。

using System.Collections.Generic;

namespace Contra3D.Core
{
    public static partial class MapLoader
    {
        #region Spawn Parsers

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

        #endregion
    }
}
