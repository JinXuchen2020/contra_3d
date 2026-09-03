using Xunit;

namespace Contra3D.Core.Tests
{
    public class HealthDamageTests
    {
        [Fact]
        public void Calculate_BasicDamage()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("enemy1", 24f);
            var (change, death) = sys.ProcessHit("enemy1", 12f);
            Assert.Equal(12f, change.DamageDealt);
            Assert.Equal(12f, change.NewHealth);
            Assert.False(change.IsDead);
        }

        [Fact]
        public void Calculate_HeadshotBonus()
        {
            var sys = new HealthDamageSystem();
            // partMultiplier=2.0 means damage is doubled
            sys.RegisterEntity("enemy1", 24f, partMultiplier: 2.0f);
            var (change, _) = sys.ProcessHit("enemy1", 12f);
            // Damage = 12 * 2.0 - 0 = 24, Health = 24 - 24 = 0
            Assert.Equal(0f, change.NewHealth);
            Assert.True(change.IsDead);
        }

        [Fact]
        public void Calculate_ArmorReduction()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("enemy1", 100f, armor: 20f);
            var (change, _) = sys.ProcessHit("enemy1", 30f);
            // Damage = 30 * 1.0 - 20 = 10, Health = 100 - 10 = 90
            Assert.Equal(90f, change.NewHealth);
        }

        [Fact]
        public void Calculate_ArmorOverrides()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("enemy1", 100f, armor: 50f);
            var (change, _) = sys.ProcessHit("enemy1", 30f);
            // Damage = 30 - 50 = -20 → 0, Health = 100
            Assert.Equal(100f, change.NewHealth);
        }

        [Fact]
        public void Death_EventsBroadcast()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            var (change, death) = sys.ProcessHit("grunt", 24f);
            Assert.True(change.IsDead);
            Assert.True(death.HasValue);
            Assert.Equal("grunt", death.Value.EntityId);
        }

        [Fact]
        public void OverDamage_HealthClampedToZero()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            var (change, _) = sys.ProcessHit("grunt", 100f);
            Assert.Equal(0f, change.NewHealth);
            Assert.True(change.IsDead);
        }

        [Fact]
        public void Dead_Entity_IgnoresDamage()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            sys.ProcessHit("grunt", 24f); // Kill
            var (change, _) = sys.ProcessHit("grunt", 10f); // Try to kill again
            Assert.Equal(0f, change.NewHealth);
        }

        [Fact]
        public void Deterministic_SameInputSameOutput()
        {
            var sys1 = new HealthDamageSystem();
            var sys2 = new HealthDamageSystem();
            sys1.RegisterEntity("e1", 100f);
            sys2.RegisterEntity("e1", 100f);
            var (c1, d1) = sys1.ProcessHit("e1", 30f);
            var (c2, d2) = sys2.ProcessHit("e1", 30f);
            Assert.Equal(c1.NewHealth, c2.NewHealth);
            Assert.Equal(c1.IsDead, c2.IsDead);
        }

        [Fact]
        public void MultipleEntities_Independent()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("e1", 50f);
            sys.RegisterEntity("e2", 100f);
            sys.ProcessHit("e1", 30f);
            sys.ProcessHit("e2", 10f);
            Assert.False(sys.IsDead("e1")); // 50 - 30 = 20, not dead
            Assert.False(sys.IsDead("e2")); // 100 - 10 = 90, not dead
        }

        [Fact]
        public void MultipleHits_KillsEnemy()
        {
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            sys.ProcessHit("grunt", 12f); // 24 - 12 = 12
            sys.ProcessHit("grunt", 12f); // 12 - 12 = 0, dead
            Assert.True(sys.IsDead("grunt"));
        }
    }
}
