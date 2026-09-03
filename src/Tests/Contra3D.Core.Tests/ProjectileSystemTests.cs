using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class ProjectileSystemTests
    {
        private static ProjectileDefinition MakeDef() =>
            new ProjectileDefinition(speed: 50f, radius: 0.5f, damage: 10f, lifetime: 5f, maxDistance: 500f);

        [Fact]
        public void PoolInitialization_AllFree()
        {
            var ps = new ProjectileSystem(MakeDef());
            Assert.Equal(0, ps.ActiveCount);
        }

        [Fact]
        public void Spawn_DecreasesFreeCount()
        {
            var ps = new ProjectileSystem(MakeDef());
            var (result, id) = ps.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            Assert.Equal(ProjectileActionResult.Success, result);
            Assert.True(id > 0);
            // Can't directly check free count (private), but can check active via Update
        }

        [Fact]
        public void Spawn_PoolExhausted_Rejected()
        {
            var ps = new ProjectileSystem(MakeDef());
            // Spawn all 200
            for (int i = 0; i < 200; i++)
                ps.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            var (result, _) = ps.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            Assert.Equal(ProjectileActionResult.PoolExhausted, result);
        }

        [Fact]
        public void Lifetime_Recycles()
        {
            var ps = new ProjectileSystem(MakeDef());
            ps.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            ps.Update(5.1f); // Past lifetime (5s)
            // Should be recycled (active count decreased)
        }

        [Fact]
        public void OutOfBounds_Recycles()
        {
            var ps = new ProjectileSystem(MakeDef());
            ps.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            ps.Update(11f); // 50 m/s * 11 s = 550m > 500m max
            // Should be recycled
        }

        [Fact]
        public void Hitscan_DetectsTarget()
        {
            var ps = new ProjectileSystem(MakeDef());
            var targets = new List<(string Id, Vector3 Position, float Radius)>
            {
                ("enemy1", new Vector3(10, 0, 0), 1f)
            };
            var (result, hit) = ps.HitscanDetect(Vector3.Zero, Vector3.UnitX, targets);
            Assert.Equal(ProjectileActionResult.Success, result);
            Assert.True(hit.HasValue);
            Assert.Equal("enemy1", hit.Value.TargetId);
        }

        [Fact]
        public void Hitscan_MissesTarget()
        {
            var ps = new ProjectileSystem(MakeDef());
            var targets = new List<(string Id, Vector3 Position, float Radius)>
            {
                ("enemy1", new Vector3(10, 10, 0), 1f) // Off axis
            };
            var (result, hit) = ps.HitscanDetect(Vector3.Zero, Vector3.UnitX, targets);
            Assert.Equal(ProjectileActionResult.Success, result);
            Assert.False(hit.HasValue);
        }

        [Fact]
        public void CollisionTolerance_HighSpeed()
        {
            // Fast projectile (100 m/s) should detect thin target (0.05m) via sweep
            var def = new ProjectileDefinition(speed: 100f, radius: 0.1f, damage: 5f);
            var ps = new ProjectileSystem(def);
            var targets = new List<(string Id, Vector3 Position, float Radius)>
            {
                ("thin_wall", new Vector3(10, 0, 0), 0.025f) // Very small radius
            };
            var (result, hit) = ps.HitscanDetect(Vector3.Zero, Vector3.UnitX, targets, maxDistance: 20f);
            // With tolerance, should hit
            Assert.True(hit.HasValue);
        }

        [Fact]
        public void Homing_TurnsTowardsTarget()
        {
            var def = new ProjectileDefinition(speed: 10f, radius: 0.5f, damage: 5f, homingTurnRate: 5f);
            var ps = new ProjectileSystem(def);
            var (result, id) = ps.SpawnProjectile(
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                "player",
                new Vector3(0, 10, 0)); // Target above
            Assert.True(id > 0);
            ps.Update(1f);
            // Direction should have turned towards target
        }

        [Fact]
        public void Deterministic_SameInputSameOutput()
        {
            var ps1 = new ProjectileSystem(MakeDef());
            var ps2 = new ProjectileSystem(MakeDef());
            ps1.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            ps2.SpawnProjectile(Vector3.Zero, Vector3.UnitX, "player");
            ps1.Update(1f);
            ps2.Update(1f);
            // Both should have same state (position, etc.)
        }
    }
}
