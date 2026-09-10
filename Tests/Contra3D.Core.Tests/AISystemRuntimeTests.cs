using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class AiRuntimeSystemRuntimeTests
    {
        private static void RegisterDefs(AiRuntimeSystem ai)
        {
            ai.RegisterDefinition(new EnemyDefinition("grunt", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, attackRange: 3f, alertThreshold: 60f));
            ai.RegisterDefinition(new EnemyDefinition("hunter", "Hunter", 18f, 5f, AiType.Chase,
                visionRange: 20f, attackRange: 2f));
            ai.RegisterDefinition(new EnemyDefinition("sniper", "Sniper", 30f, 0f, AiType.Sniper,
                visionRange: 35f, attackRange: 30f));
            ai.RegisterDefinition(new EnemyDefinition("rusher", "Rusher", 12f, 6f, AiType.Rusher,
                visionRange: 12f, attackRange: 1f));
        }

        // ---- RegisterDefinition ----

        [Fact]
        public void RegisterDefinition_Null_ThrowsArgumentNullException()
        {
            var ai = new AiRuntimeSystem();
            Assert.Throws<ArgumentNullException>(() => ai.RegisterDefinition(null!));
        }

        [Fact]
        public void RegisterDefinition_AllowsSpawn()
        {
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("test", "Test", 10f, 1f, AiType.Patrol));
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(ai.TrySpawn("test", new Vector3(10, 0, 10)));
        }

        // ---- TrySpawn ----

        [Fact]
        public void TrySpawn_UnknownEnemy_ReturnsFalse()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            Assert.False(ai.TrySpawn("nonexistent", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_TooCloseToPlayer_ReturnsFalse()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.False(ai.TrySpawn("grunt", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_NormalCapEnqueues()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 12; i++)
                ai.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            Assert.Equal(12, ai.ActiveCount);
            Assert.False(ai.TrySpawn("grunt", new Vector3(50, 0, 50)));
            Assert.Equal(12, ai.ActiveCount);
        }

        [Fact]
        public void TrySpawn_RusherCapEnqueues()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 4; i++)
                ai.TrySpawn("rusher", new Vector3(10 + i, 0, 10));
            Assert.Equal(4, ai.RusherCount);
            Assert.False(ai.TrySpawn("rusher", new Vector3(20, 0, 20)));
            Assert.Equal(4, ai.RusherCount);
        }

        [Fact]
        public void TrySpawn_Successful_SetsInitialState()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            bool spawned = ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(spawned);
            var state = ai.GetStates()["grunt"];
            Assert.Equal(AiState.Idle, state.State);
            Assert.Equal(24f, state.Health);
        }

        [Fact]
        public void TrySpawn_AcceptableDistance_ReturnsTrue()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.True(ai.TrySpawn("grunt", new Vector3(10, 0, 0)));
        }

        [Fact]
        public void TrySpawn_Boundary_5m_IsAccepted()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.True(ai.TrySpawn("grunt", new Vector3(5f, 0, 0)));
        }

        [Fact]
        public void TrySpawn_JustInsideBoundary_4_9m_IsRejected()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.False(ai.TrySpawn("grunt", new Vector3(4.9f, 0, 0)));
        }

        // ---- OnEnemyDead ----

        [Fact]
        public void OnEnemyDead_RemovesEntity()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(ai.GetStates().ContainsKey("grunt"));
            ai.OnEnemyDead("grunt");
            Assert.False(ai.GetStates().ContainsKey("grunt"));
        }

        [Fact]
        public void OnEnemyDead_UnknownId_IsNoOp()
        {
            var ai = new AiRuntimeSystem();
            ai.OnEnemyDead("nobody");
        }

        [Fact]
        public void OnEnemyDead_ReleasesQueuedSpawn()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 12; i++)
                ai.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            ai.TrySpawn("grunt", new Vector3(50, 0, 50));
            Assert.Equal(12, ai.ActiveCount);
            var ids = new List<string>(ai.GetStates().Keys);
            ai.OnEnemyDead(ids[0]);
            Assert.Equal(12, ai.ActiveCount);
        }

        // ---- Update ----

        [Fact]
        public void Update_Patrol_GainsVigilanceNearPlayer()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(10, 0, 0));
            ai.TrySpawn("grunt", new Vector3(5, 0, 5));
            ai.Update(2f);
            var state = ai.GetStates()["grunt"];
            Assert.True(state.Vigilance >= 30, $"vigilance={state.Vigilance}");
        }

        [Fact]
        public void Update_Chase_EntersChaseOrAlert()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(10, 0, 0));
            ai.TrySpawn("hunter", new Vector3(5, 0, 5));
            ai.Update(1f);
            var state = ai.GetStates()["hunter"];
            Assert.True(state.State == AiState.Chase || state.State == AiState.Alert, $"got {state.State}");
        }

        [Fact]
        public void Update_IgnoresInvalidDt()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            int countBefore = ai.ActiveCount;
            ai.Update(0f);
            ai.Update(float.NaN);
            ai.Update(float.PositiveInfinity);
            Assert.Equal(countBefore, ai.ActiveCount);
        }

        [Fact]
        public void Update_Rusher_ReachesPlayer_Dies()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            ai.TrySpawn("rusher", new Vector3(1, 0, 0));
            ai.Update(1f);
            Assert.False(ai.GetStates().ContainsKey("rusher"));
        }

        [Fact]
        public void Update_Patrol_VigilanceDecaysOutOfSight()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(5, 0, 0));
            ai.TrySpawn("grunt", Vector3.Zero);
            ai.Update(4f);
            var preState = ai.GetStates()["grunt"];
            Assert.Equal(AiState.Alert, preState.State);

            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.Update(8f);
            var postState = ai.GetStates()["grunt"];
            Assert.True(postState.Vigilance < 60f, $"vigilance={postState.Vigilance:F2}");
        }

        [Fact]
        public void Update_Sniper_EntersAimState()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(32, 0, 0));
            ai.TrySpawn("sniper", Vector3.Zero);
            ai.Update(1f);
            var state = ai.GetStates()["sniper"];
            Assert.Equal(AiState.Aim, state.State);
        }

        // ---- GetCommand ----

        [Fact]
        public void GetCommand_UnknownEnemy_ReturnsIdle()
        {
            var ai = new AiRuntimeSystem();
            var cmd = ai.GetCommand("nobody");
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void GetCommand_IdleState_ReturnsIdle()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            var cmd = ai.GetCommand("grunt");
            Assert.Equal(AICommand.Idle.MoveIntent, cmd.MoveIntent);
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void GetCommand_OutOfRange_ReturnsIdle()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(5, 0, 5));
            var cmd = ai.GetCommand("grunt");
            Assert.False(cmd.FireRequest);
        }

        // ---- SetPlayerPosition ----

        [Fact]
        public void SetPlayerPosition_AffectsSpawnDistanceCheck()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.False(ai.TrySpawn("grunt", new Vector3(2, 0, 0)));
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(ai.TrySpawn("grunt", new Vector3(2, 0, 2)));
        }

        // ---- GetStates ----

        [Fact]
        public void GetStates_ReturnsCurrentStates()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            ai.TrySpawn("hunter", new Vector3(11, 0, 11));
            var states = ai.GetStates();
            Assert.True(states.ContainsKey("grunt"));
            Assert.True(states.ContainsKey("hunter"));
        }

        [Fact]
        public void GetStates_EmptyWhenNoSpawn()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            var states = ai.GetStates();
            Assert.Empty(states);
        }

        [Fact]
        public void GetStates_RemovesDeadEnemy()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(ai.GetStates().ContainsKey("grunt"));
            ai.OnEnemyDead("grunt");
            Assert.False(ai.GetStates().ContainsKey("grunt"));
        }

        // ---- ActiveCount / RusherCount ----

        [Fact]
        public void ActiveCount_IncreasesWithSpawn()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = ai.ActiveCount;
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, ai.ActiveCount);
        }

        [Fact]
        public void ActiveCount_DecreasesWithDead()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            int before = ai.ActiveCount;
            ai.OnEnemyDead("grunt");
            Assert.Equal(before - 1, ai.ActiveCount);
        }

        [Fact]
        public void RusherCount_IncreasesWithRusherSpawn()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = ai.RusherCount;
            ai.TrySpawn("rusher", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, ai.RusherCount);
        }

        [Fact]
        public void RusherCount_DecreasesWithRusherDead()
        {
            var ai = new AiRuntimeSystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("rusher", new Vector3(10, 0, 10));
            int before = ai.RusherCount;
            ai.OnEnemyDead("rusher");
            Assert.Equal(before - 1, ai.RusherCount);
        }

        // ---- Instance Isolation Tests ----

        [Fact]
        public void Instances_AreIsolated_RegisterDefinition()
        {
            var ai1 = new AiRuntimeSystem();
            var ai2 = new AiRuntimeSystem();
            ai1.RegisterDefinition(new EnemyDefinition("test", "Test", 10f, 1f, AiType.Patrol));
            ai1.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(ai1.TrySpawn("test", new Vector3(10, 0, 10)));
            Assert.False(ai2.TrySpawn("test", new Vector3(10, 0, 10))); // ai2 doesn't have the definition
        }

        [Fact]
        public void Instances_AreIsolated_State()
        {
            var ai1 = new AiRuntimeSystem();
            var ai2 = new AiRuntimeSystem();
            RegisterDefs(ai1);
            RegisterDefs(ai2);
            ai1.SetPlayerPosition(new Vector3(100, 0, 0));
            ai2.SetPlayerPosition(new Vector3(100, 0, 0));
            ai1.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.Equal(1, ai1.ActiveCount);
            Assert.Equal(0, ai2.ActiveCount); // ai2 unaffected
        }

        [Fact]
        public void Instances_AreIsolated_Config()
        {
            var config1 = AISpawnConfig.LoadFromString("max_normal: 5\nmax_rusher: 2\nanti_door_camping_distance: 3");
            var config2 = AISpawnConfig.LoadFromString("max_normal: 20\nmax_rusher: 10\nanti_door_camping_distance: 10");
            var ai1 = new AiRuntimeSystem(config1);
            var ai2 = new AiRuntimeSystem(config2);
            RegisterDefs(ai1);
            RegisterDefs(ai2);
            ai1.SetPlayerPosition(new Vector3(100, 0, 0));
            ai2.SetPlayerPosition(new Vector3(100, 0, 0));

            // ai1 hits cap at 5
            for (int i = 0; i < 5; i++) ai1.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            Assert.False(ai1.TrySpawn("grunt", new Vector3(20, 0, 20)));

            // ai2 cap is 20
            for (int i = 0; i < 10; i++) ai2.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            Assert.True(ai2.TrySpawn("grunt", new Vector3(20, 0, 20)));
        }

        [Fact]
        public void Instances_AreIsolated_RandomProvider()
        {
            var fixedRandom = new DeterministicRandomProvider(42);
            var ai1 = new AiRuntimeSystem(randomProvider: fixedRandom);
            var ai2 = new AiRuntimeSystem(randomProvider: fixedRandom);
            RegisterDefs(ai1);
            RegisterDefs(ai2);
            ai1.SetPlayerPosition(new Vector3(100, 0, 0));
            ai2.SetPlayerPosition(new Vector3(100, 0, 0));
            ai1.TrySpawn("grunt", new Vector3(10, 0, 10));
            ai2.TrySpawn("grunt", new Vector3(10, 0, 10));

            // Both should have same patrol target due to same seed
            var state1 = ai1.GetStates()["grunt"];
            var state2 = ai2.GetStates()["grunt"];
            Assert.Equal(state1.PatrolTarget, state2.PatrolTarget);

            ai1.Update(1f);
            ai2.Update(1f);

            var state1After = ai1.GetStates()["grunt"];
            var state2After = ai2.GetStates()["grunt"];
            Assert.Equal(state1After.Position, state2After.Position);
        }

        // ---- BDD: rg_enemy_patrol_alert_combat_chain ----

        // Full patrol → alert → combat → death encounter chain.
        // Verifies patrol_alert_combat_full_encounter BDD scenario (T-BDD-ADOPT-e4175b).
        [Fact]
        public void rg_enemy_patrol_alert_combat_chain_FullEncounterChain()
        {
            // Setup: create AiRuntimeSystem, register grunt_soldier definition, set player near enemy
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("grunt_soldier", "Grunt Soldier", 24f, 2f, AiType.Patrol,
                visionRange: 15f, attackRange: 3f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f, vigilanceDecayPerSecond: 10f));

            // Enemy at [0,0,0], player at [5,0,0] — within 15m vision range
            ai.SetPlayerPosition(new Vector3(5, 0, 0));
            bool spawned = ai.TrySpawn("grunt_soldier", Vector3.Zero);
            Assert.True(spawned, "Enemy should spawn successfully");

            var healthSys = new HealthDamageSystem();
            healthSys.RegisterEntity("grunt_soldier", 24f);

            // Phase 1: Simulate time until vigilance reaches Alert threshold (60)
            // Vigilance gain = 20/s, so 60 requires 3 seconds
            const float dt = 1f;
            ai.Update(dt); // t=1: vigilance=20, state=Idle
            ai.Update(dt); // t=2: vigilance=40, state=Idle
            ai.Update(dt); // t=3: vigilance=60, state=Alert

            var stateAfterAlert = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Alert, stateAfterAlert.State);
            Assert.True(stateAfterAlert.Vigilance >= 60f);
            Assert.True(ai.ActiveCount >= 1);

            // Phase 2: Simulate time until vigilance reaches Combat threshold (100)
            // From 60 to 100 at +20/s = 2 more seconds
            ai.Update(dt); // t=4: vigilance=80, still Alert
            ai.Update(dt); // t=5: vigilance=100, state=Combat

            var stateAfterCombat = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Combat, stateAfterCombat.State);
            Assert.True(stateAfterCombat.Vigilance >= 100f);

            // Phase 3: Apply lethal damage and verify death
            var (change, death) = healthSys.ProcessHit("grunt_soldier", 24f, killerId: "player", dropTableId: "grunt_drop");
            Assert.True(change.IsDead);
            Assert.True(death.HasValue);
            Assert.Equal("grunt_soldier", death.Value.EntityId);
            Assert.Equal("player", death.Value.KillerId);

            // Phase 4: Notify AiRuntimeSystem of death and verify active_count decreases
            int activeBefore = ai.ActiveCount;
            ai.OnEnemyDead("grunt_soldier");
            Assert.Equal(activeBefore - 1, ai.ActiveCount);
            Assert.False(ai.GetStates().ContainsKey("grunt_soldier"));
        }

        // Verifies the enemy starts in Idle/Patrol state before any player proximity triggers
        [Fact]
        public void rg_enemy_patrol_alert_combat_chain_StartsInIdleState()
        {
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("grunt_soldier", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f));

            // Player far away — no detection
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.TrySpawn("grunt_soldier", new Vector3(0, 0, 0));

            var state = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Idle, state.State);
            Assert.Equal(0f, state.Vigilance);
            Assert.Equal(1, ai.ActiveCount);
        }

        // Verifies vigilance decay when player moves out of vision range
        [Fact]
        public void rg_enemy_patrol_alert_combat_chain_VigilanceDecaysOutOfSight()
        {
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("grunt_soldier", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f, vigilanceDecayPerSecond: 10f));

            // Player within vision range
            ai.SetPlayerPosition(new Vector3(5, 0, 0));
            ai.TrySpawn("grunt_soldier", new Vector3(0, 0, 0));

            // Build vigilance to Alert
            ai.Update(3f);
            var stateAlert = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Alert, stateAlert.State);

            // Player moves far away — out of 15m vision range
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            ai.Update(5f); // 5s decay at 10/s = 50 points lost

            var stateDecayed = ai.GetStates()["grunt_soldier"];
            Assert.True(stateDecayed.Vigilance < 60f, $"vigilance should decay below alert threshold, got {stateDecayed.Vigilance:F2}");
        }

        // Verifies that a Patrol enemy reaches Alert and then Combat states
        // as vigilance builds up while the player is visible.
        [Fact]
        public void rg_enemy_patrol_alert_combat_chain_PatrolToAlertToCombat()
        {
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("grunt_soldier", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, attackRange: 3f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f));

            // Spawn enemy at distance > 5m to pass anti-camping check
            ai.SetPlayerPosition(new Vector3(8, 0, 0));
            ai.TrySpawn("grunt_soldier", new Vector3(0, 0, 0));

            // Phase 1: Idle -> Alert (vigilance 0 -> 60, needs 3s at +20/s)
            ai.Update(3f);
            var alertState = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Alert, alertState.State);
            Assert.True(alertState.Vigilance >= 60f);

            // Phase 2: Alert -> Combat (vigilance 60 -> 100, needs 2 more s)
            ai.Update(2f);
            var combatState = ai.GetStates()["grunt_soldier"];
            Assert.Equal(AiState.Combat, combatState.State);
            Assert.True(combatState.Vigilance >= 100f);

            // Phase 3: Verify fire request in combat state
            // Player must be within attack range for GetCommand to return FireRequest
            ai.SetPlayerPosition(new Vector3(2, 0, 0));
            ai.Update(0.5f);
            var cmd = ai.GetCommand("grunt_soldier");
            Assert.True(cmd.FireRequest);
        }

        // Full chain with death event and downstream active_count verification
        [Fact]
        public void rg_enemy_patrol_alert_combat_chain_DeathDecreasesActiveCount()
        {
            var ai = new AiRuntimeSystem();
            ai.RegisterDefinition(new EnemyDefinition("grunt_soldier", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, attackRange: 3f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f));

            ai.SetPlayerPosition(new Vector3(5, 0, 0));
            ai.TrySpawn("grunt_soldier", new Vector3(0, 0, 0));
            int initialActive = ai.ActiveCount;
            Assert.True(initialActive >= 1);

            // Kill the enemy directly
            ai.OnEnemyDead("grunt_soldier");
            Assert.Equal(initialActive - 1, ai.ActiveCount);
            Assert.False(ai.GetStates().ContainsKey("grunt_soldier"));
        }
    }
}