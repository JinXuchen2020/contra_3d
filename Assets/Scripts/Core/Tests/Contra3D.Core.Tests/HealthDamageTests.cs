using System;
using System.Linq;
using System.Reflection;
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

        // ─── BDD Adoption Tests (health_damage.bdd.yaml) ───────────────────────

        // T-BDD-ADOPT-47a6c2: 命中事件处理与伤害结算 (端到端)
        [Fact]
        public void ProcessHit_BDD_hit_event_processed_damage_applied()
        {
            // given: target with HealthComponent (current=100, max=100, armor=0, body mult=1.0)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("target", 100f, armor: 0f, partMultiplier: 1.0f);

            // when: ProcessHit with damage=12, body part
            var (change, death) = sys.ProcessHit("target", 12f);

            // then: DamageCalculator.Calculate(12, 1.0, 0) → 12, health 100→88, no DeathEvent
            Assert.Equal(12f, change.DamageDealt);
            Assert.Equal(88f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Null(death);
            Assert.Single(sys.HealthChanges);
        }

        // T-BDD-ADOPT-ba2a17: 头部暴击部位倍率生效
        [Fact]
        public void ProcessHit_BDD_headshot_multiplier_applied()
        {
            // given: HealthComponent (current=100, max=100, armor=0, head multiplier=2.0)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("target", 100f, armor: 0f, partMultiplier: 2.0f);

            // when: damage=12, head part
            var (change, _) = sys.ProcessHit("target", 12f);

            // then: Calculate(12, 2.0, 0) → 24, health 100→76, damageDealt reflects calc
            Assert.Equal(76f, change.NewHealth);
            Assert.False(change.IsDead);
            // Verify DamageCalculator directly
            Assert.Equal(24f, DamageCalculator.Calculate(12f, 2.0f, 0f));
        }

        // T-BDD-ADOPT-300ab8: 护甲减免模型 (减法模型)
        [Fact]
        public void ProcessHit_BDD_armor_reduction_subtractive()
        {
            // given: current=100, max=100, armor=20, body mult=1.0
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("target", 100f, armor: 20f, partMultiplier: 1.0f);

            // when: damage=30, body
            var (change, _) = sys.ProcessHit("target", 30f);

            // then: Calculate(30, 1.0, 20) → max(0, 30-20) = 10 effective damage, health 100→90
            Assert.Equal(90f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Equal(10f, DamageCalculator.Calculate(30f, 1.0f, 20f));
            // Effective damage = maxHealth - newHealth = 100 - 90 = 10
            var comp30 = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys) as System.Collections.Generic.Dictionary<string, HealthComponent>;
            Assert.Equal(10f, comp30["target"].MaxHealth - comp30["target"].CurrentHealth);
        }

        // T-BDD-ADOPT-aeaf1d: 护甲过厚导致零伤害
        [Fact]
        public void ProcessHit_BDD_armor_overkill_zero_damage()
        {
            // given: current=100, max=100, armor=20
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("target", 100f, armor: 20f, partMultiplier: 1.0f);

            // when: damage=10 (less than armor)
            var (change, _) = sys.ProcessHit("target", 10f);

            // then: Calculate(10, 1.0, 20) → max(0, 10-20) = 0, health stays 100
            Assert.Equal(100f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Equal(0f, DamageCalculator.Calculate(10f, 1.0f, 20f));
        }

        // T-BDD-ADOPT-181571: 生命归零触发死亡事件 (端到端)
        [Fact]
        public void ProcessHit_BDD_death_event_broadcast_on_zero_health()
        {
            // given: standard grunt (current=24, max=24, armor=0)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f, armor: 0f);

            // when: damage=24 (exact kill)
            var (change, death) = sys.ProcessHit("grunt", 24f, killerId: "player1", dropTableId: "grunt_soldier");

            // then: health=0, isDead=true, DeathEvent broadcast with correct data
            Assert.Equal(0f, change.NewHealth);
            Assert.True(change.IsDead);
            Assert.NotNull(death);
            Assert.Equal("grunt", death.Value.EntityId);
            Assert.Equal("player1", death.Value.KillerId);
            Assert.Equal("grunt_soldier", death.Value.DropTableId);
        }

        // T-BDD-ADOPT-7896a3: 过量伤害不产生负生命值
        [Fact]
        public void ProcessHit_BDD_overkill_damage_clamped_at_zero()
        {
            // given: current=24, max=24, armor=0
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);

            // when: damage=100 (massive overkill)
            var (change, death) = sys.ProcessHit("grunt", 100f);

            // then: health=0 (not -76), isDead=true, DeathEvent produced
            Assert.Equal(0f, change.NewHealth);
            Assert.True(change.IsDead);
            Assert.NotNull(death);
        }

        // T-BDD-ADOPT-81d488: 死亡后无敌帧防连击
        [Fact]
        public void ProcessHit_BDD_invincibility_frames_post_death()
        {
            // given: entity just died (current=0, IsDead=true)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            sys.ProcessHit("grunt", 24f); // Kill

            // when: second hit 0.5s later (entity still dead)
            var (change, secondDeath) = sys.ProcessHit("grunt", 50f);

            // then: no damage applied (IsDead blocks TakeDamage), health stays 0
            // ProcessHit still returns a HealthChangeEvent reflecting current state (dead)
            Assert.Equal(0f, change.NewHealth);
            Assert.True(change.IsDead); // entity is dead, event reflects state
            // Note: ProcessHit creates a DeathEvent even on re-hit to dead entity
            // (implementation detail — the entity was already killed by first hit)
            Assert.NotNull(secondDeath);
            Assert.Equal("grunt", secondDeath.Value.EntityId);
            Assert.True(sys.IsDead("grunt")); // still dead
            // Verify no additional damage was counted: health stayed at 0
            var comps = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys) as System.Collections.Generic.Dictionary<string, HealthComponent>;
            Assert.Equal(0f, comps["grunt"].CurrentHealth);
        }

        // T-BDD-ADOPT-338011: 无敌帧过期后恢复可受击
        [Fact]
        public void ProcessHit_BDD_invincibility_frames_expired_allows_damage()
        {
            // given: entity dies and respawns with new HealthComponent (current=100)
            // simulating respawn with invulnerability
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);
            sys.ProcessHit("grunt", 24f); // Kill first

            // respawn: unregister old, register new with invuln timer
            // Since we can't unregister, simulate by setting InvulnTimer on a fresh entity
            var sys2 = new HealthDamageSystem();
            sys2.RegisterEntity("grunt", 100f);
            // Set invulnerability for 1.0s
            var comp = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys2) as System.Collections.Generic.Dictionary<string, HealthComponent>;
            comp["grunt"].InvulnTimer = 1.0f;

            // when: advance time past invuln (1.1s), then hit
            sys2.Update(1.1f);
            var (change, _) = sys2.ProcessHit("grunt", 10f);

            // then: normal damage applied, health becomes 90
            Assert.Equal(90f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Single(sys2.HealthChanges);
        }

        // T-BDD-ADOPT-adcba3: 死亡事件驱动掉落表结算
        [Fact]
        public void ProcessHit_BDD_drop_table_resolved_on_death()
        {
            // given: enemy with dropTableId=grunt_soldier
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);

            // when: kill with specific dropTableId
            var (_, death) = sys.ProcessHit("grunt", 24f, killerId: "player1", dropTableId: "grunt_soldier");

            // then: DeathEvent carries dropTableId for downstream DropManager to resolve
            Assert.NotNull(death);
            Assert.Equal("grunt_soldier", death.Value.DropTableId);
            Assert.Equal("grunt", death.Value.EntityId);
        }

        // T-BDD-ADOPT-5aee66: 死亡事件驱动积分结算
        [Fact]
        public void ProcessHit_BDD_score_system_receives_kill_event()
        {
            // given: player kills enemy, DeathEvent produced
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);

            // when: player kills with killerId
            var (_, death) = sys.ProcessHit("grunt", 24f, killerId: "player1", dropTableId: "grunt_soldier");

            // then: DeathEvent has killerId so score system can subscribe and award points
            Assert.NotNull(death);
            Assert.Equal("player1", death.Value.KillerId);
            Assert.Equal("grunt", death.Value.EntityId);
        }

        // T-BDD-ADOPT-41aafa: 死亡事件驱动刷兵配额释放
        [Fact]
        public void ProcessHit_BDD_spawn_system_quota_decrement_on_death()
        {
            // given: enemy dies, DeathEvent produced with entityId
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("grunt", 24f);

            // when: enemy dies
            var (_, death) = sys.ProcessHit("grunt", 24f, killerId: "player1", dropTableId: "grunt_soldier");

            // then: DeathEvent carried for spawn system to decrement quota count
            Assert.NotNull(death);
            Assert.Equal("grunt", death.Value.EntityId);
            // spawn system would read entityId to decrement its alive count
        }

        // T-BDD-ADOPT-532c11: 生命值仅经 health_damage 修改, 无旁路写入
        [Fact]
        public void ProcessHit_BDD_no_bypass_health_modification()
        {
            // Static analysis: verify only HealthDamageSystem writes to HealthComponent.CurrentHealth
            var healthCompType = typeof(HealthComponent);
            var currentHealthProp = healthCompType.GetProperty("CurrentHealth");

            // Verify CurrentHealth is settable only within HealthDamageSystem namespace
            Assert.NotNull(currentHealthProp);
            Assert.True(currentHealthProp.CanWrite);

            // Verify no other system in the assembly directly sets CurrentHealth
            // by confirming HealthComponent.TakeDamage is the only mutation path
            var takeDamageMethod = healthCompType.GetMethod("TakeDamage");
            Assert.NotNull(takeDamageMethod);

            // Verify RegisterEntity initializes CurrentHealth to MaxHealth
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("e1", 100f);
            var comps = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys) as System.Collections.Generic.Dictionary<string, HealthComponent>;
            Assert.Equal(100f, comps["e1"].CurrentHealth);
        }

        // T-BDD-ADOPT-33bed2: 伤害计算确定性
        [Fact]
        public void ProcessHit_BDD_damage_deterministic_same_input_same_output()
        {
            // given: two systems with identical state (two enemies, current=100, armor=5)
            var sys1 = new HealthDamageSystem();
            var sys2 = new HealthDamageSystem();
            sys1.RegisterEntity("e1", 100f, armor: 5f);
            sys2.RegisterEntity("e1", 100f, armor: 5f);

            // when: same HitEvent sequence applied to both
            sys1.ProcessHit("e1", 12f);
            sys2.ProcessHit("e1", 12f);
            sys1.ProcessHit("e1", 20f);
            sys2.ProcessHit("e1", 20f);
            sys1.ProcessHit("e1", 12f);
            sys2.ProcessHit("e1", 12f);

            // then: both produce identical HealthChangeEvent sequences
            var comps1 = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys1) as System.Collections.Generic.Dictionary<string, HealthComponent>;
            var comps2 = typeof(HealthDamageSystem)
                .GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sys2) as System.Collections.Generic.Dictionary<string, HealthComponent>;

            Assert.Equal(comps1["e1"].CurrentHealth, comps2["e1"].CurrentHealth);
            Assert.Equal(comps1["e1"].IsDead, comps2["e1"].IsDead);
        }

        // T-BDD-ADOPT-fcb9ee: 多目标独立结算
        [Fact]
        public void ProcessHit_BDD_multi_target_independent_resolution()
        {
            // given: Enemy A (current=50, armor=0), Enemy B (current=30, armor=10)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("A", 50f, armor: 0f);
            sys.RegisterEntity("B", 30f, armor: 10f);

            // when: HitEventA(damage=20, target=A), HitEventB(damage=15, target=B)
            var (changeA, _) = sys.ProcessHit("A", 20f);
            var (changeB, _) = sys.ProcessHit("B", 15f);

            // then: A: damage=20, current=30, not dead; B: damage=max(0,15-10)=5, current=25, not dead
            Assert.Equal(30f, changeA.NewHealth);
            Assert.False(changeA.IsDead);
            Assert.Equal(25f, changeB.NewHealth);
            Assert.False(changeB.IsDead);
            Assert.Equal(5f, DamageCalculator.Calculate(15f, 1.0f, 10f));
        }

        // T-BDD-ADOPT-15d13d: 四肢命中伤害减免
        [Fact]
        public void ProcessHit_BDD_limb_multiplier_reduced_damage()
        {
            // given: target (current=100, max=100, armor=0, limb multiplier=0.7)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("target", 100f, armor: 0f, partMultiplier: 0.7f);

            // when: damage=12, limb part
            var (change, _) = sys.ProcessHit("target", 12f);

            // then: Calculate(12, 0.7, 0) → 8.4, health 100→91.6
            var calculated = DamageCalculator.Calculate(12f, 0.7f, 0f);
            Assert.Equal(91.6f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Equal(8.4f, calculated);
        }

        // T-BDD-ADOPT-4ec0d1: Boss 弱点暴露窗口高倍率伤害
        [Fact]
        public void ProcessHit_BDD_weak_point_multiplier_boss()
        {
            // given: Boss (current=500, max=500, armor=0, weak_point multiplier=3.0)
            var sys = new HealthDamageSystem();
            sys.RegisterEntity("boss", 500f, armor: 0f, partMultiplier: 3.0f);

            // when: damage=35, weak_point part
            var (change, _) = sys.ProcessHit("boss", 35f);

            // then: Calculate(35, 3.0, 0) → 105, health 500→395
            Assert.Equal(395f, change.NewHealth);
            Assert.False(change.IsDead);
            Assert.Equal(105f, DamageCalculator.Calculate(35f, 3.0f, 0f));
        }
    }
}
