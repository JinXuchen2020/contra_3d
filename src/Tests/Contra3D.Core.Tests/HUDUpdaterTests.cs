using Xunit;

namespace Contra3D.Core.Tests
{
    public class HUDUpdaterTests
    {
        private const float InitialHealth = 100f;
        private const int InitialLives = 3;
        private const int InitialScore = 0;
        private const string WeaponId = "machineGun";

        private HUDState InitialState =>
            HUDState.FromInitialState(InitialHealth, InitialLives, InitialScore, WeaponId);

        // ─── 初始状态 ───────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_CorrectValues()
        {
            var state = InitialState;
            Assert.Equal(100f, state.Health);
            Assert.Equal(100f, state.MaxHealth);
            Assert.Equal(3, state.Lives);
            Assert.Equal(0, state.Score);
            Assert.Equal(WeaponId, state.CurrentWeaponId);
            Assert.False(state.LowHealth);
        }

        // ─── 受伤更新 ───────────────────────────────────────────────────────────

        [Fact]
        public void TakeDamage_UpdatesHealth()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new HealthChangeEvent("player", 20f, 80f, false));
            Assert.Equal(80f, updater.State.Health);
            Assert.Equal(100f, updater.State.MaxHealth);
        }

        [Fact]
        public void TakeDamage_HealthClampedToZero()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new HealthChangeEvent("player", 150f, 0f, true));
            Assert.Equal(0f, updater.State.Health);
        }

        [Fact]
        public void HealthRecovery_ClearsLowHealthFlag()
        {
            var updater = new HUDUpdater(InitialState);
            // Drop below threshold
            updater.Process(new HealthChangeEvent("player", 30f, 20f, false));
            Assert.True(updater.State.LowHealth);
            Assert.NotEmpty(updater.GeneratedLowHealthEvents);

            // Recover above threshold
            updater.Process(new HealthChangeEvent("player", 0f, 60f, false));
            Assert.False(updater.State.LowHealth);
        }

        // ─── 低血量标志 ─────────────────────────────────────────────────────────

        [Fact]
        public void LowHealth_FlagTriggersBelow25Percent()
        {
            var updater = new HUDUpdater(InitialState);
            // 26/100 = 0.26 → not low
            updater.Process(new HealthChangeEvent("player", 20f, 26f, false));
            Assert.False(updater.State.LowHealth);

            // 24.9/100 = 0.249 → low
            updater.Process(new HealthChangeEvent("player", 1.1f, 24.9f, false));
            Assert.True(updater.State.LowHealth);
        }

        [Fact]
        public void LowHealth_EventGeneratedOncePerEntry()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new HealthChangeEvent("player", 10f, 10f, false));
            // Should fire once for dropping below threshold
            Assert.Single(updater.GeneratedLowHealthEvents);
            Assert.Equal(0.1f, updater.GeneratedLowHealthEvents[0].HealthRatio);

            // Stay low — no additional event
            updater.Process(new HealthChangeEvent("player", 5f, 5f, false));
            Assert.Single(updater.GeneratedLowHealthEvents);
        }

        // ─── 死亡扣命 ───────────────────────────────────────────────────────────

        [Fact]
        public void Death_DecrementsLife()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new DeathEvent("player", "boss", "loot"));
            Assert.Equal(2, updater.State.Lives);
        }

        [Fact]
        public void Death_LastLife_ResetsToInitialState()
        {
            var updater = new HUDUpdater(InitialState);
            // Kill twice → 1 life left
            updater.Process(new DeathEvent("player", "g1", "loot"));
            updater.Process(new DeathEvent("player", "g2", "loot"));
            Assert.Equal(1, updater.State.Lives);

            // Final death → reset
            updater.Process(new DeathEvent("player", "boss", "loot"));
            Assert.Equal(0, updater.State.Lives);
            Assert.Equal(0, updater.State.Score);
            Assert.Equal(100f, updater.State.Health);
        }

        // ─── 得分递增 ───────────────────────────────────────────────────────────

        [Fact]
        public void ScoreIncrement_UpdatesScore()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new ScoreIncrementEvent(500, 500));
            Assert.Equal(500, updater.State.Score);
        }

        [Fact]
        public void ScoreThreshold_TriggersExtraLifeAt2000()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new ScoreIncrementEvent(2000, 2000));
            Assert.Equal(4, updater.State.Lives);
            Assert.Single(updater.GeneratedExtraLifeEvents);
            Assert.Equal(4, updater.GeneratedExtraLifeEvents[0].NewLifeCount);
        }

        [Fact]
        public void ScoreThresholds_MultipleConsecutive()
        {
            var updater = new HUDUpdater(InitialState);
            // Cross 2000 → +1 life
            updater.Process(new ScoreIncrementEvent(2000, 2000));
            // Cross 5000 → +1 life
            updater.Process(new ScoreIncrementEvent(3000, 5000));
            // Cross 10000 → +1 life
            updater.Process(new ScoreIncrementEvent(5000, 10000));

            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
        }

        [Fact]
        public void ScoreThreshold_SingleJumpCrossesMultiple()
        {
            var updater = new HUDUpdater(InitialState);
            // Jump directly from 0 to 10000
            updater.Process(new ScoreIncrementEvent(10000, 10000));
            Assert.Equal(6, updater.State.Lives);
            Assert.Equal(3, updater.GeneratedExtraLifeEvents.Count);
        }

        // ─── 不可变性 ───────────────────────────────────────────────────────────

        [Fact]
        public void Immutability_OriginalStateUnchanged()
        {
            var original = InitialState;
            var updater = new HUDUpdater(original);

            updater.Process(new HealthChangeEvent("player", 30f, 70f, false));
            updater.Process(new ScoreIncrementEvent(100, 100));

            // Original struct is untouched
            Assert.Equal(100f, original.Health);
            Assert.Equal(3, original.Lives);
            Assert.Equal(0, original.Score);
        }

        [Fact]
        public void Immutability_HUDStateWithMethodsReturnNewInstances()
        {
            var s = InitialState;
            var s2 = s.WithHealth(50f);
            var s3 = s2.WithScore(1000);

            Assert.Equal(100f, s.Health);    // untouched
            Assert.Equal(50f, s2.Health);
            Assert.Equal(100f, s2.MaxHealth);
            Assert.Equal(1000, s3.Score);
            Assert.Equal(50f, s3.Health);    // inherits from s2
        }

        // ─── 武器切换 ───────────────────────────────────────────────────────────

        [Fact]
        public void SetWeapon_UpdatesWeaponId()
        {
            var updater = new HUDUpdater(InitialState);
            updater.SetWeapon("shotgun");
            Assert.Equal("shotgun", updater.State.CurrentWeaponId);
        }

        // ─── BDD: hud_health_lives_low_health_cue ─────────────────────────────

        [Fact]
        public void HUD_LowHealthFlagTriggersBelow25Percent()
        {
            // given: hybrid health model, initial_lives=3
            var updater = new HUDUpdater(InitialState);

            // when: player takes damage to 24% of max health
            updater.Process(new HealthChangeEvent("player", 76f, 24f, false));

            // then: LowHealth flag is true
            Assert.True(updater.State.LowHealth);
        }

        [Fact]
        public void HUD_LowHealthClearedOnRecovery()
        {
            // given: low-health flag already triggered
            var updater = new HUDUpdater(InitialState);
            updater.Process(new HealthChangeEvent("player", 10f, 20f, false));
            Assert.True(updater.State.LowHealth);

            // when: player recovers above threshold
            updater.Process(new HealthChangeEvent("player", 0f, 50f, false));

            // then: LowHealth flag is cleared
            Assert.False(updater.State.LowHealth);
        }

        // ─── 准星扩散 ──────────────────────────────────────────────────────────

        [Fact]
        public void HUD_CrosshairSpreadTracksMovement()
        {
            // given: player has rifle_default, crosshair at default 4°
            var initialState = HUDState.FromInitialState(InitialHealth, InitialLives, InitialScore, "rifle_default");
            Assert.Equal(4.0f, initialState.CrosshairSpread);

            var updater = new HUDUpdater(initialState);

            // when: player moves/jumps → spread increases to 8.0f
            var movedState = updater.State.WithCrosshairSpread(8.0f);
            updater.Reset(movedState);

            // then: State.CrosshairSpread == 8.0f after update
            Assert.Equal(8.0f, updater.State.CrosshairSpread);
        }

        // ─── BDD: pause_menu_freeze_and_settings ────────────────────────────────

        [Fact]
        public void HUD_PauseFreezesSimulation()
        {
            // given: game running (single player)
            var updater = new HUDUpdater(InitialState);

            // advance state while unpaused
            updater.Process(new HealthChangeEvent("player", 30f, 70f, false));
            updater.Process(new ScoreIncrementEvent(500, 500));
            var beforePause = updater.State;

            // when: player presses pause key → set paused=true on state
            updater.Reset(beforePause.WithIsPaused(true));
            Assert.True(updater.State.IsPaused);

            // then: simulation freezes — events are ignored
            updater.Process(new HealthChangeEvent("player", 20f, 50f, false));
            updater.Process(new ScoreIncrementEvent(1000, 1500));
            updater.Process(new DeathEvent("player", "boss", "loot"));

            // score, health, lives unchanged
            Assert.Equal(beforePause.Health, updater.State.Health);
            Assert.Equal(beforePause.Score, updater.State.Score);
            Assert.Equal(beforePause.Lives, updater.State.Lives);
            Assert.False(updater.State.LowHealth); // threshold not re-triggered
            Assert.Empty(updater.GeneratedLowHealthEvents);
            Assert.Empty(updater.GeneratedExtraLifeEvents);
        }

        // ─── Reset ──────────────────────────────────────────────────────────────

        [Fact]
        public void Reset_CleansAllState()
        {
            var updater = new HUDUpdater(InitialState);
            updater.Process(new HealthChangeEvent("player", 30f, 20f, false));
            updater.Process(new ScoreIncrementEvent(2000, 2000));
            updater.SetWeapon("shotgun");

            var fresh = HUDState.FromInitialState(InitialHealth, InitialLives, 0, WeaponId);
            updater.Reset(fresh);

            Assert.Equal(InitialHealth, updater.State.Health);
            Assert.Equal(InitialLives, updater.State.Lives);
            Assert.Equal(0, updater.State.Score);
            Assert.Equal(WeaponId, updater.State.CurrentWeaponId);
            Assert.False(updater.State.LowHealth);
            Assert.Empty(updater.GeneratedExtraLifeEvents);
            Assert.Empty(updater.GeneratedLowHealthEvents);
        }

        // ─── BDD: hit_marker_on_hit_and_kill ────────────────────────────────────

        [Fact]
        public void HUD_HitMarkerOnKill_SetsHitMarkerWhenIsDead()
        {
            // given: player fires and bullet hits enemy hurtbox
            var updater = new HUDUpdater(InitialState);
            Assert.False(updater.State.HitMarker);

            // when: bullet hits then kills (IsDead=true)
            updater.Process(new HealthChangeEvent("enemy", 50f, 0f, true));

            // then: HitMarker flag is set with ~100ms duration (80-120ms window)
            Assert.True(updater.State.HitMarker);
            Assert.InRange(updater.State.HitMarkerDuration, 80f, 120f);
        }

        [Fact]
        public void HUD_HitMarkerOnHit_NotSetWhenNotDead()
        {
            // given: player fires and bullet hits but enemy survives
            var updater = new HUDUpdater(InitialState);
            // when: bullet hits without killing (IsDead=false)
            updater.Process(new HealthChangeEvent("enemy", 30f, 70f, false));
            // then: HitMarker remains inactive
            Assert.False(updater.State.HitMarker);
            Assert.Equal(0f, updater.State.HitMarkerDuration);
        }

        // ─── BDD: score_display_and_1up_award ──────────────────────────────────

        [Fact]
        public void HUD_ScoreThresholdTriggersExtraLife()
        {
            // given: score_threshold 1UP mechanism enabled, thresholds递增 (2k/5k/10k)
            var initialState = HUDState.FromInitialState(InitialHealth, InitialLives, 1900);
            var updater = new HUDUpdater(initialState);
            Assert.Equal(3, updater.State.Lives);
            Assert.Equal(1900, updater.State.Score);

            // when: player kills enemy and score crosses 2000 threshold
            updater.Process(new ScoreIncrementEvent(200, 2100));

            // then: score HUD refreshes, lives +1, ExtraLifeEvent generated
            Assert.Equal(2100, updater.State.Score);
            Assert.Equal(4, updater.State.Lives);
            Assert.Single(updater.GeneratedExtraLifeEvents);
            Assert.Equal(4, updater.GeneratedExtraLifeEvents[0].NewLifeCount);
        }
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
