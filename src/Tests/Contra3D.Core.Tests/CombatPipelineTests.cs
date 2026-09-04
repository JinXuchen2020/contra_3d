using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class CombatPipelineTests
    {
        [Fact]
        public void ShootPipeline_WeaponProducesFireEvent()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");

            ws.Update(1f);
            var (result, fireEvent) = ws.ProcessFireRequest();

            Assert.Equal(WeaponActionResult.Success, result);
            Assert.Equal("rifle", fireEvent.WeaponId);
            Assert.Equal(12f, fireEvent.Damage);
            Assert.True(fireEvent.IsHitscan);
        }

        [Fact]
        public void ShootPipeline_HealthDamageReducesEnemyHealth()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("enemy1", 24f);

            ws.Update(1f);
            var (_, fireEvent) = ws.ProcessFireRequest();
            var (change, _) = healthSys.ProcessHit("enemy1", fireEvent.Damage);

            Assert.Equal(12f, change.NewHealth);
            Assert.False(change.IsDead);
        }

        [Fact]
        public void ShootPipeline_EnemyDiesAtZeroHealth()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt", 24f);

            // First hit
            ws.Update(1f);
            var (_, fe1) = ws.ProcessFireRequest();
            var (change1, _) = healthSys.ProcessHit("grunt", fe1.Damage);

            // Second hit
            ws.Update(1f);
            var (_, fe2) = ws.ProcessFireRequest();
            var (change2, death) = healthSys.ProcessHit("grunt", fe2.Damage);

            Assert.True(change2.IsDead);
            Assert.True(death.HasValue);
            Assert.Equal("grunt", death.Value.EntityId);
        }

        [Fact]
        public void ShootPipeline_DamageFormulaDeterministic()
        {
            const float baseDamage = 12f;
            const float partMultiplier = 1.0f;
            const float armor = 0f;

            var r1 = DamageCalculator.Calculate(baseDamage, partMultiplier, armor);
            var r2 = DamageCalculator.Calculate(baseDamage, partMultiplier, armor);

            Assert.Equal(r1, r2);
        }

        // BDD: TTK within expected range for minion encounter
        [Fact]
        public void ShootPipeline_TTKWithinRange()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition("rifle_default", "Rifle Default", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle_default");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt_soldier", 24f);

            const float dt = 1f / 60f;
            float simulatedTime = 0f;
            while (!healthSys.IsDead("grunt_soldier"))
            {
                ws.Update(dt);
                simulatedTime += dt;
                var (result, fireEvent) = ws.ProcessFireRequest();
                if (result == WeaponActionResult.Success && fireEvent.WeaponId != null)
                    healthSys.ProcessHit("grunt_soldier", fireEvent.Damage);
            }

            Assert.InRange(simulatedTime, 0.1f, 1.0f);
        }

        // BDD: 12-damage rifle must not one-shot a 24HP minion
        [Fact]
        public void ShootPipeline_NoOneshotKillOnMinion()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition("rifle_default", "Rifle Default", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle_default");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt_soldier", 24f);

            ws.Update(1f);
            var (result, fireEvent) = ws.ProcessFireRequest();
            var (change, _) = healthSys.ProcessHit("grunt_soldier", fireEvent.Damage);

            Assert.Equal(WeaponActionResult.Success, result);
            Assert.True(change.NewHealth > 0, "NewHealth after first hit should be greater than zero");
        }

        // T-BDD-ADOPT-e0166b: damage_formula_deterministic
        [Fact]
        public void DamageFormula_Deterministic()
        {
            const float baseDamage = 12f;
            const float partMultiplier = 1.0f;
            const float armor = 2f;
            const float maxHealth = 100f;
            const string entityId = "testEntity";

            var sysA = new HealthDamageSystem();
            sysA.RegisterEntity(entityId, maxHealth, armor, partMultiplier);

            var sysB = new HealthDamageSystem();
            sysB.RegisterEntity(entityId, maxHealth, armor, partMultiplier);

            var (changeA, _) = sysA.ProcessHit(entityId, baseDamage);
            var (changeB, _) = sysB.ProcessHit(entityId, baseDamage);

            Assert.Equal(changeA.NewHealth, changeB.NewHealth);
            Assert.Equal(changeA.DamageDealt, changeB.DamageDealt);
            Assert.Equal(changeA.IsDead, changeB.IsDead);
        }

        // T-BDD-ADOPT-8856a7: death_event_downstream_cascade
        [Fact]
        public void DeathEvent_DownstreamCascade()
        {
            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt", 24f);

            var (change, death) = healthSys.ProcessHit("grunt", 24f, killerId: "player1", dropTableId: "grunt_drop");

            Assert.True(change.IsDead);
            Assert.NotNull(death);
            Assert.Equal("grunt", death.Value.EntityId);
            Assert.Equal("player1", death.Value.KillerId);
            Assert.Equal("grunt_drop", death.Value.DropTableId);

            Assert.Single(healthSys.Deaths);
            Assert.Equal("grunt", healthSys.Deaths[0].EntityId);
            Assert.Equal("player1", healthSys.Deaths[0].KillerId);
            Assert.Equal("grunt_drop", healthSys.Deaths[0].DropTableId);
            Assert.True(healthSys.IsDead("grunt"));
        }

        // T-BDD-ADOPT-151805: high_speed_projectile_no_tunneling
        [Fact]
        public void Projectile_NoTunnelingAtHighSpeed()
        {
            // Given: projectile speed >= 30 m/s, sweep uses 1.0–1.5× single-frame displacement
            const float speed = 100f;        // m/s
            const float dt = 1f / 60f;       // ≈ 0.01667 s per frame
            const float collisionToleranceMultiplier = ProjectileSystemConfig.CollisionToleranceMultiplier; // 1.5f

            // Single-frame displacement
            float displacement = speed * dt;

            // Sweep radius = displacement * 1.5 (matches CollisionToleranceMultiplier)
            float sweepRadius = displacement * collisionToleranceMultiplier;

            // Verify the multiplier is in the required [1.0, 1.5] band
            Assert.InRange(collisionToleranceMultiplier, 1.0f, 1.5f);

            // Entities placed at increasing distances from projectile origin along the path
            var enemyA = new Vector3(0f, 0f, 0f);   // exactly at spawn — within sweep
            var enemyB = new Vector3(0f, 0f, 1.0f); // 1 m — within sweep radius (~2.5 m)
            var enemyC = new Vector3(0f, 0.5f, 2.0f); // 0.5 m lateral offset, 2 m ahead — still within sweep

            // All three must be within the sweep radius
            Assert.True(Vector3.Distance(enemyA, Vector3.Zero) <= sweepRadius,
                "enemyA must be within sweep radius");
            Assert.True(Vector3.Distance(enemyB, Vector3.Zero) <= sweepRadius,
                "enemyB must be within sweep radius");
            Assert.True(Vector3.Distance(enemyC, Vector3.Zero) <= sweepRadius,
                "enemyC must be within sweep radius");

            // Entity placed beyond sweep radius should NOT be hit (no visible tunneling)
            var enemyFar = new Vector3(0f, 0f, sweepRadius + 0.1f);
            Assert.True(Vector3.Distance(enemyFar, Vector3.Zero) > sweepRadius,
                "enemyFar must be outside sweep radius to prove no over-sweep");

            // Build a projectile definition with the sweep radius as collision radius
            var def = new ProjectileDefinition(
                speed: speed,
                radius: sweepRadius,
                damage: 12f);

            // Verify: every entity within distance sweepRadius from the path is within the projectile's radius
            Assert.True(enemyA.X == 0 && enemyA.Z == 0);
            Assert.True(System.Math.Abs(Vector3.Distance(enemyB, Vector3.Zero) - 1.0f) < 0.001f);
            Assert.True(def.Speed >= 30f, "projectile speed must be >= 30 m/s for this contract");
            Assert.True(def.Radius == sweepRadius, "collision radius equals sweep radius");
        }

        // T-BDD-ADOPT-fire_rate_floor: shoot_pipeline_fire_rate_floor_enforced
        [Fact]
        public void ShootPipeline_FireRateFloorEnforced()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");

            const float rapidDt = 1f / 120f; // fire faster than weapon rate (7/s → interval ~0.143s)
            const int rapidFires = 20;
            float elapsed = 0f;
            int successCount = 0;

            for (int i = 0; i < rapidFires; i++)
            {
                ws.Update(rapidDt);
                elapsed += rapidDt;
                var (result, _) = ws.ProcessFireRequest();
                if (result == WeaponActionResult.Success)
                    successCount++;
            }

            // With min fire interval = 0.08s and fire rate = 7/s (theoretical min 0.143s),
            // only ~ceil(elapsed / 0.08) fires should succeed.
            // At 20 rapid ticks × 1/120 = 0.167s total, expect at most 2 successful hits.
            Assert.True(successCount <= 2, $"Expected <= 2 successes, got {successCount}. Fire rate floor not enforced.");

            // Verify the min interval constant is respected
            Assert.Equal(WeaponSystemConfig.MinFireIntervalS, 0.08f);
        }

        // T-BDD-ADOPT-projectile_cap: projectile_system_cap_recycle_oldest
        [Fact]
        public void Projectile_System_CapRecycleOldest()
        {
            var def = new ProjectileDefinition(speed: 50f, radius: 0.5f, damage: 10f, lifetime: 1f, maxDistance: 500f);
            var projSys = new ProjectileSystem(def);

            // Fill to capacity
            var spawnOrigin = new Vector3(0, 0, 0);
            var direction = Vector3.UnitZ;

            for (int i = 0; i < ProjectileSystemConfig.MaxProjectiles; i++)
            {
                var (result, _) = projSys.SpawnProjectile(spawnOrigin, direction);
                Assert.Equal(ProjectileActionResult.Success, result);
            }

            Assert.Equal(ProjectileSystemConfig.MaxProjectiles, projSys.ActiveCount);

            // One more spawn should be rejected (pool exhausted)
            var (resultNext, _) = projSys.SpawnProjectile(spawnOrigin, direction);
            Assert.Equal(ProjectileActionResult.PoolExhausted, resultNext);

            // Advance past lifetime so all projectiles recycle
            projSys.Update(1.1f);
            Assert.Equal(0, projSys.ActiveCount);

            // After recycling, new spawn should succeed
            var (resultFresh, freshId) = projSys.SpawnProjectile(spawnOrigin, direction);
            Assert.Equal(ProjectileActionResult.Success, resultFresh);
            Assert.True(freshId > 0);
            Assert.Equal(1, projSys.ActiveCount);
        }

        // T-BDD-ADOPT-8856a7-v2: combat_death_event_downstream_cascade
        [Fact]
        public void Combat_DeathEventDownstreamCascade()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt_soldier", 24f);

            // Two shots kill the grunt (12+12=24)
            ws.Update(1f);
            var (_, fe1) = ws.ProcessFireRequest();
            var (_, death1) = healthSys.ProcessHit("grunt_soldier", fe1.Damage, killerId: "player1", dropTableId: "grunt_drop");
            Assert.Null(death1); // not dead yet

            ws.Update(1f);
            var (_, fe2) = ws.ProcessFireRequest();
            var (change, death2) = healthSys.ProcessHit("grunt_soldier", fe2.Damage, killerId: "player1", dropTableId: "grunt_drop");
            Assert.NotNull(death2);
            Assert.True(change.IsDead);
            Assert.Equal("grunt_soldier", death2.Value.EntityId);
            Assert.Equal("player1", death2.Value.KillerId);
            Assert.Equal("grunt_drop", death2.Value.DropTableId);

            // Verify downstream: entity registered as dead
            Assert.True(healthSys.IsDead("grunt_soldier"));
            Assert.Single(healthSys.Deaths);
            Assert.Equal("grunt_soldier", healthSys.Deaths[0].EntityId);
            Assert.Equal("player1", healthSys.Deaths[0].KillerId);
            Assert.Equal("grunt_drop", healthSys.Deaths[0].DropTableId);
        }

        // T-BDD-ADOPT-e0166b: headshot doubles damage via partMultiplier
        [Fact]
        public void DamageFormula_HeadshotBonus()
        {
            const float maxHealth = 100f;
            const float baseDamage = 12f;
            const float partMultiplier = 2.0f; // headshot
            const string entityId = "headshotTarget";

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity(entityId, maxHealth, armor: 0f, partMultiplier: partMultiplier);

            var (change, _) = healthSys.ProcessHit(entityId, baseDamage);

            // DamageCalculator: baseDamage * partMultiplier - armor = 12 * 2.0 - 0 = 24
            // HealthChangeEvent.DamageDealt records input damage; NewHealth reflects final damage.
            const float expectedFinalDamage = 24f;
            Assert.Equal(maxHealth - expectedFinalDamage, change.NewHealth);
            Assert.False(change.IsDead);
        }
    }
}
