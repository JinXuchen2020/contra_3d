using System.Collections.Generic;
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
    }
}
