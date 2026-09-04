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
