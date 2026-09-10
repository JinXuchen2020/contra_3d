using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    /// <summary>地图验证逻辑 — 从 MapLoader 提取，保持核心职责单一。</summary>
    /// <remarks>维度 1 修复：将 ValidateMap 从 487 行单文件拆出，降低 MapLoader 体积。</remarks>
    internal static class MapValidator
    {
        /// <summary>已知的有效拾取物 ID 集合（来自武器/道具定义数据）。</summary>
        internal static readonly HashSet<string> ValidPickupIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "p_weapon_shotgun",
            "p_weapon_laser",
            "p_weapon_heavy_machinegun",
            "p_health_small",
            "p_health_large",
            "p_ammo_rifle",
            "p_armor_light",
            "p_powerup_barrier"
        };

        /// <summary>验证地图定义，返回全部错误列表（空表示通过）。</summary>
        public static List<MapValidationError> Validate(MapLoader.ParsedMap m)
        {
            var errors = new List<MapValidationError>();
            float bound = m.CollisionBoundX > 0f ? m.CollisionBoundX : MapLoader.DefaultCollisionBoundX;

            if (m.SpawnPoints.Count < 2)
                errors.Add(new MapValidationError("spawn_points",
                    $"Need at least 2 spawn points, got {m.SpawnPoints.Count}."));

            for (int i = 0; i < m.SpawnPoints.Count; i++)
            {
                var sp = m.SpawnPoints[i];
                if (Math.Abs(sp.X) > bound)
                    errors.Add(new MapValidationError($"spawn_points[{i}].x",
                        $"Coordinate {sp.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(sp.Z) > bound)
                    errors.Add(new MapValidationError($"spawn_points[{i}].z",
                        $"Coordinate {sp.Z} is outside collision boundary ±{bound}."));

                for (int j = i + 1; j < m.SpawnPoints.Count; j++)
                {
                    float dist = sp.DistanceTo(m.SpawnPoints[j]);
                    if (dist < MapLoader.MinSpawnDistance)
                        errors.Add(new MapValidationError($"spawn_points[{i}]↔spawn_points[{j}]",
                            $"Distance {dist:F2}m is below minimum {MapLoader.MinSpawnDistance}m."));
                }
            }

            int minCoverCount = (m.SpawnPoints.Count + 1) / 2;
            if (m.CoverPoints.Count < minCoverCount)
                errors.Add(new MapValidationError("cover_points",
                    $"Need at least {minCoverCount} cover points (≥50% of {m.SpawnPoints.Count} spawns), got {m.CoverPoints.Count}."));

            for (int i = 0; i < m.CoverPoints.Count; i++)
            {
                var cp = m.CoverPoints[i];
                if (Math.Abs(cp.X) > bound)
                    errors.Add(new MapValidationError($"cover_points[{i}].x",
                        $"Coordinate {cp.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(cp.Z) > bound)
                    errors.Add(new MapValidationError($"cover_points[{i}].z",
                        $"Coordinate {cp.Z} is outside collision boundary ±{bound}."));
            }

            if (m.PickupLocations.Count > 20)
                errors.Add(new MapValidationError("pickup_locations",
                    $"Too many pickups: {m.PickupLocations.Count} (max 20)."));

            for (int i = 0; i < m.PickupLocations.Count; i++)
            {
                var pl = m.PickupLocations[i];
                if (Math.Abs(pl.X) > bound)
                    errors.Add(new MapValidationError($"pickup_locations[{i}].x",
                        $"Coordinate {pl.X} is outside collision boundary ±{bound}."));
                if (Math.Abs(pl.Z) > bound)
                    errors.Add(new MapValidationError($"pickup_locations[{i}].z",
                        $"Coordinate {pl.Z} is outside collision boundary ±{bound}."));

                // Validate spawn_id reference against known valid IDs
                if (!string.IsNullOrWhiteSpace(pl.SpawnId) && !ValidPickupIds.Contains(pl.SpawnId))
                    errors.Add(new MapValidationError($"pickup_locations[{i}].spawn_id",
                        $"Reference '{pl.SpawnId}' is not a known valid pickup ID."));
            }

            return errors;
        }
    }
}
