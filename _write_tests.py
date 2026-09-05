import re

path = r'src\Tests\Contra3D.Core.Tests\HUDUpdaterTests.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.rstrip().split('\n')
base = '\n'.join(lines[:-2])

bdd = r"""
        // ---- BDD: hud_initial_state (T-BDD-ADOPT-6253be) ----

        [Fact]
        public void HUD_InitState_CorrectValues_BDD_T6253be()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            Assert.Equal(100f, updater.State.Health);
            Assert.Equal(100f, updater.State.MaxHealth);
            Assert.Equal(3, updater.State.Lives);
            Assert.Equal(0, updater.State.Score);
            Assert.Equal("rifle_default", updater.State.CurrentWeaponId);
            Assert.False(updater.State.LowHealth);
        }

        // ---- BDD: hud_health_update_on_damage (T-BDD-ADOPT-a24844) ----

        [Fact]
        public void HUD_HealthUpdate_OnDamage_BDD_Ta24844()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var original = initialState;
            var updater = new HUDUpdater(initialState);
            updater.Process(new HealthChangeEvent("player", 30f, 70f, false));
            Assert.Equal(70f, updater.State.Health);
            Assert.Equal(100f, updater.State.MaxHealth);
            Assert.False(updater.State.LowHealth);
            Assert.NotEqual(original, updater.State);
        }

        // ---- BDD: hud_low_health_flag_trigger (T-BDD-ADOPT-e4c384) ----

        [Fact]
        public void HUD_LowHealthFlag_TriggeredBelow25pct_BDD_Te4c384()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new HealthChangeEvent("player", 70f, 30f, false));
            Assert.False(updater.State.LowHealth);
            Assert.Empty(updater.GeneratedLowHealthEvents);
            updater.Process(new HealthChangeEvent("player", 10f, 20f, false));
            Assert.Equal(20f, updater.State.Health);
            Assert.True(updater.State.LowHealth);
            Assert.Single(updater.GeneratedLowHealthEvents);
            Assert.InRange(updater.GeneratedLowHealthEvents[0].HealthRatio, 0.19f, 0.21f);
        }

        // ---- BDD: hud_death_decrements_lives (T-BDD-ADOPT-085420) ----

        [Fact]
        public void HUD_Death_DecrementsLives_BDD_T085420()
        {
            var initialState = HUDState.FromInitialState(1f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new DeathEvent("player", "enemy_grunt", "loot_table"));
            Assert.Equal(2, updater.State.Lives);
            Assert.Equal(1f, updater.State.Health);
        }

        [Fact]
        public void HUD_Death_LastLife_TriggerGameOver_BDD_T085420()
        {
            var initialState = HUDState.FromInitialState(100f, 1, 5000, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new DeathEvent("player", "boss", "loot_table"));
            Assert.Equal(0, updater.State.Lives);
            Assert.Equal(100f, updater.State.Health);
            Assert.Equal(5000, updater.State.Score);
        }

        // ---- BDD: hud_score_increment_and_extra_life (T-BDD-ADOPT-de7a62) ----

        [Fact]
        public void HUD_ScoreIncrement_1UPTresholdCrossed_BDD_Tde7a62()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new ScoreIncrementEvent(2000, 2000));
            updater.Process(new ScoreIncrementEvent(3000, 5000));
            updater.Process(new ScoreIncrementEvent(14500, 19500));
            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
            updater.Process(new ScoreIncrementEvent(600, 20100));
            Assert.Equal(20100, updater.State.Score);
            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
        }

        [Fact]
        public void HUD_ScoreIncrement_NextThresholdAdvances_BDD_Tde7a62()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new ScoreIncrementEvent(2000, 2000));
            updater.Process(new ScoreIncrementEvent(3000, 5000));
            updater.Process(new ScoreIncrementEvent(5000, 10000));
            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
            updater.Process(new ScoreIncrementEvent(90000, 100000));
            Assert.Equal(100000, updater.State.Score);
            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
        }

        // ---- BDD: hud_weapon_change_reflects (T-BDD-ADOPT-4e320d) ----

        [Fact]
        public void HUD_WeaponChange_ReflectsToHUD_BDD_T4e320d()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var original = initialState;
            var updater = new HUDUpdater(initialState);
            updater.SetWeapon("spread_shot");
            Assert.Equal("spread_shot", updater.State.CurrentWeaponId);
            Assert.NotEqual(original, updater.State);
            Assert.Equal("rifle_default", original.CurrentWeaponId);
        }

        // ---- BDD: hud_concurrent_events_batch_process (T-BDD-ADOPT-93b81f) ----

        [Fact]
        public void HUD_ConcurrentEvents_BatchProcessed_MergedState_BDD_T93b81f()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new HealthChangeEvent("player", 20f, 80f, false));
            updater.Process(new ScoreIncrementEvent(500, 500));
            updater.SetWeapon("spread_shot");
            Assert.Equal(80f, updater.State.Health);
            Assert.Equal(500, updater.State.Score);
            Assert.Equal("spread_shot", updater.State.CurrentWeaponId);
            Assert.Equal(3, updater.State.Lives);
            Assert.False(updater.State.LowHealth);
        }

        [Fact]
        public void HUD_ConcurrentEvents_NoLeakToRenderer_BDD_T93b81f()
        {
            var initialState = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(initialState);
            updater.Process(new HealthChangeEvent("player", 20f, 80f, false));
            var afterDamage = updater.State;
            updater.Process(new ScoreIncrementEvent(500, 500));
            var afterScore = updater.State;
            Assert.Equal(80f, afterDamage.Health);
            Assert.Equal(500, afterScore.Score);
            Assert.Equal(80f, afterScore.Health);
            Assert.Equal(500, afterScore.Score);
        }

        // ---- BDD: hud_state_immutability (T-BDD-ADOPT-f9c88f) ----

        [Fact]
        public void HUD_StateImmutability_OriginalUnchanged_BDD_Tf9c88f()
        {
            var stateA = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var updater = new HUDUpdater(stateA);
            updater.Process(new HealthChangeEvent("player", 10f, 90f, false));
            var stateB = updater.State;
            Assert.Equal(100f, stateA.Health);
            Assert.Equal(90f, stateB.Health);
            Assert.NotEqual(stateA, stateB);
        }

        [Fact]
        public void HUD_StateImmutability_ChainUpdatesPreserveOriginal_BDD_Tf9c88f()
        {
            var stateA = HUDState.FromInitialState(100f, 3, 0, "rifle_default");
            var stateB = stateA.WithHealth(90f);
            var stateC = stateB.WithScore(500);
            var stateD = stateC.WithWeapon("spread_shot");
            Assert.Equal(100f, stateA.Health);
            Assert.Equal(0, stateA.Score);
            Assert.Equal("rifle_default", stateA.CurrentWeaponId);
            Assert.Equal(90f, stateB.Health);
            Assert.Equal(0, stateB.Score);
            Assert.Equal(90f, stateC.Health);
            Assert.Equal(500, stateC.Score);
            Assert.Equal("spread_shot", stateD.CurrentWeaponId);
            Assert.Equal(500, stateD.Score);
        }
    }
}
"""

final = base + bdd
with open(path, 'w', encoding='utf-8') as f:
    f.write(final)

tests = re.findall(r'public void (\w+)', final)
print(f'Total: {len(tests)}')
bdd_t = [t for t in tests if 'BDD' in t]
print(f'BDD: {len(bdd_t)}')
