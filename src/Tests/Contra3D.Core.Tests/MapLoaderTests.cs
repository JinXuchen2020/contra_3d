// T-SYS-007 (map_loading/Tests) — MapLoader 单元测试。
// 设计来源: templates/system_design/map_loading.md。

using System;
using System.Collections.Generic;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class MapLoaderTests
    {
        #region Valid map loading

        [Fact]
        public void Load_ValidMap_ReturnsMapDefinition()
        {
            string yaml = @"
maps:
  - map_id: m_test01
    name: ""测试地图""
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 0, team: enemy}
      - {x: 20, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 2}
      - {x: 15, y: 0, z: -2}
    pickup_locations:
      - {x: 5, y: 1, z: 0, type: weapon}
      - {x: 15, y: 0, z: 1, type: health}
    navmesh: ""
";
            MapDefinition def = MapLoader.LoadFromString(yaml);
            Assert.Equal("m_test01", def.MapId);
            Assert.Equal("测试地图", def.Name);
            Assert.Equal(3, def.SpawnPoints.Length);
            Assert.Equal(2, def.CoverPoints.Length);
            Assert.Equal(2, def.PickupLocations.Length);
            Assert.Equal(SpawnTeam.Player, def.SpawnPoints[0].Team);
            Assert.Equal(SpawnTeam.Enemy, def.SpawnPoints[1].Team);
            Assert.Equal(PickupType.Weapon, def.PickupLocations[0].Type);
            Assert.Equal(PickupType.Health, def.PickupLocations[1].Type);
            Assert.Equal(MapLoader.DefaultCollisionBoundX, def.CollisionBoundX);
        }

        [Fact]
        public void Load_UsesCustomCollisionBoundX()
        {
            string yaml = @"
maps:
  - map_id: m_tiny
    name: Tiny
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 5, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 2, y: 0, z: 0}
    pickup_locations: []
    collision_bound_x: 10.0
    navmesh: ""
";
            MapDefinition def = MapLoader.LoadFromString(yaml);
            Assert.Equal(10.0f, def.CollisionBoundX);
        }

        #endregion

        #region Insufficient spawn points

        [Fact]
        public void Load_TooFewSpawnPoints_Throws()
        {
            string yaml = @"
maps:
  - map_id: m_one
    name: OneSpawn
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
    cover_points: []
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("at least 2 spawn points", ex.Message);
        }

        #endregion

        #region Spawn points too close

        [Fact]
        public void Load_SpawnPointsTooClose_Throws()
        {
            string yaml = @"
maps:
  - map_id: m_close
    name: Close
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 2, y: 0, z: 0, team: enemy}
      - {x: 4, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 1, y: 0, z: 0}
      - {x: 3, y: 0, z: 0}
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("below minimum", ex.Message);
        }

        #endregion

        #region Out-of-bounds coordinates

        [Fact]
        public void Load_SpawnOutsideBoundary_Throws()
        {
            // With default bound 25.0, x=30 exceeds boundary
            string yaml = @"
maps:
  - map_id: m_big
    name: Big
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 30, y: 0, z: 0, team: enemy}
      - {x: -30, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 0}
      - {x: 10, y: 0, z: 0}
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("outside collision boundary", ex.Message);
        }

        [Fact]
        public void Load_CoverPointOutsideBoundary_Throws()
        {
            string yaml = @"
maps:
  - map_id: m_badcover
    name: BadCover
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 0, team: enemy}
      - {x: 20, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 0}
      - {x: 30, y: 0, z: 0}
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("outside collision boundary", ex.Message);
        }

        [Fact]
        public void Load_PickupOutsideBoundary_Throws()
        {
            string yaml = @"
maps:
  - map_id: m_badpickup
    name: BadPickup
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 0, team: enemy}
      - {x: 20, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 0}
      - {x: 15, y: 0, z: 0}
    pickup_locations:
      - {x: 5, y: 1, z: 0, type: weapon}
      - {x: 30, y: 0, z: 1, type: health}
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("outside collision boundary", ex.Message);
        }

        #endregion

        #region Pickup overflow

        [Fact]
        public void Load_TooManyPickups_Throws()
        {
            // Build YAML with 21 pickups
            var pickups = new List<string>();
            for (int i = 0; i < 21; i++)
                pickups.Add($"      - {{x: {i}, y: 0, z: 0, type: weapon}}");

            string yaml = @"
maps:
  - map_id: m_toomany
    name: TooMany
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 0, team: enemy}
      - {x: 20, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 0}
      - {x: 15, y: 0, z: 0}
    pickup_locations:
" + string.Join("\n", pickups) + @"
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("Too many pickups", ex.Message);
        }

        #endregion

        #region Missing / insufficient cover points

        [Fact]
        public void Load_TooFewCoverPoints_Throws()
        {
            // 3 spawn points → need ≥ 2 cover points (ceil(3/2)=2)
            // But we give only 1 → should fail
            string yaml = @"
maps:
  - map_id: m_nocover
    name: NoCover
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 0, team: enemy}
      - {x: 20, y: 0, z: 0, team: enemy}
    cover_points:
      - {x: 5, y: 0, z: 0}
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            Assert.Contains("cover points", ex.Message);
        }

        #endregion

        #region Multiple errors collected

        [Fact]
        public void Load_MultipleErrors_AllCollected()
        {
            // Too few spawns (1) + out-of-bounds + no cover → all reported
            string yaml = @"
maps:
  - map_id: m_multi
    name: Multi
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
    cover_points: []
    pickup_locations: []
    navmesh: ""
";
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(yaml));
            // Should have at least 2 errors: spawn count + cover count
            Assert.True(ex.Errors.Count >= 2, $"Expected ≥2 errors, got {ex.Errors.Count}: {ex.Message}");
        }

        #endregion

        #region Empty / invalid YAML

        [Fact]
        public void Load_EmptyYAML_Throws()
        {
            var ex = Assert.Throws<MapLoader.MapLoadException>(() => MapLoader.LoadFromString(""));
            Assert.Contains("No map entries", ex.Message);
        }

        [Fact]
        public void Load_NonExistentFile_ThrowsFileNotFoundException()
        {
            var ex = Assert.Throws<System.IO.FileNotFoundException>(
                () => MapLoader.Load("C:\\nonexistent\\map.yaml"));
            Assert.Contains("not found", ex.Message);
        }

        #endregion

        #region MapDefinition immutability

        [Fact]
        public void MapDefinition_ArbitraryAccess_DoesNotThrow()
        {
            string yaml = @"
maps:
  - map_id: m_immutable
    name: Immutable
    spawn_points:
      - {x: 0, y: 0, z: 0, team: player}
      - {x: 10, y: 0, z: 5, team: enemy}
    cover_points:
      - {x: 3, y: 0, z: 2}
    pickup_locations:
      - {x: 5, y: 1, z: 0, type: ammo}
    navmesh: ""
";
            MapDefinition def = MapLoader.LoadFromString(yaml);

            Assert.Equal("m_immutable", def.MapId);
            Assert.Equal("Immutable", def.Name);
            Assert.Equal(2, def.SpawnPoints.Length);
            Assert.Single(def.CoverPoints);
            Assert.Single(def.PickupLocations);
            Assert.Equal(0f, def.SpawnPoints[0].X);
            Assert.Equal(5f, def.SpawnPoints[1].Z);
            Assert.Equal(SpawnTeam.Enemy, def.SpawnPoints[1].Team);
            Assert.Equal(PickupType.Ammo, def.PickupLocations[0].Type);
            Assert.Equal(MapLoader.DefaultCollisionBoundX, def.CollisionBoundX);
        }

        #endregion
    }
}
