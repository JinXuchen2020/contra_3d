using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Contra3D.AI;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class AISystemRuntimeTests
    {
        private static void RegisterDefs(AISystem ai)
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
            var ai = new AISystem();
            Assert.Throws<ArgumentNullException>(() => ai.RegisterDefinition(null!));
        }

        [Fact]
        public void RegisterDefinition_AllowsSpawn()
        {
            var ai = new AISystem();
            ai.RegisterDefinition(new EnemyDefinition("test", "Test", 10f, 1f, AiType.Patrol));
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(ai.TrySpawn("test", new Vector3(10, 0, 10)));
        }

        // ---- TrySpawn ----

        [Fact]
        public void TrySpawn_UnknownEnemy_ReturnsFalse()
        {
            var ai = new AISystem();
            RegisterDefs(ai);
            Assert.False(ai.TrySpawn("nonexistent", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_TooCloseToPlayer_ReturnsFalse()
        {
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.False(ai.TrySpawn("grunt", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_NormalCapEnqueues()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.True(ai.TrySpawn("grunt", new Vector3(10, 0, 0)));
        }

        [Fact]
        public void TrySpawn_Boundary_5m_IsAccepted()
        {
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.True(ai.TrySpawn("grunt", new Vector3(5f, 0, 0)));
        }

        [Fact]
        public void TrySpawn_JustInsideBoundary_4_9m_IsRejected()
        {
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            Assert.False(ai.TrySpawn("grunt", new Vector3(4.9f, 0, 0)));
        }

        // ---- OnEnemyDead ----

        [Fact]
        public void OnEnemyDead_RemovesEntity()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
            ai.OnEnemyDead("nobody");
        }

        [Fact]
        public void OnEnemyDead_ReleasesQueuedSpawn()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(Vector3.Zero);
            ai.TrySpawn("rusher", new Vector3(1, 0, 0));
            ai.Update(1f);
            Assert.False(ai.GetStates().ContainsKey("rusher"));
        }

        [Fact]
        public void Update_Patrol_VigilanceDecaysOutOfSight()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
            var cmd = ai.GetCommand("nobody");
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void GetCommand_IdleState_ReturnsIdle()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
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
            var ai = new AISystem();
            RegisterDefs(ai);
            var states = ai.GetStates();
            Assert.Empty(states);
        }

        [Fact]
        public void GetStates_RemovesDeadEnemy()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = ai.ActiveCount;
            ai.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, ai.ActiveCount);
        }

        [Fact]
        public void ActiveCount_DecreasesWithDead()
        {
            var ai = new AISystem();
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
            var ai = new AISystem();
            RegisterDefs(ai);
            ai.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = ai.RusherCount;
            ai.TrySpawn("rusher", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, ai.RusherCount);
        }

        [Fact]
        public void RusherCount_DecreasesWithRusherDead()
        {
            var ai = new AISystem();
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
            var ai1 = new AISystem();
            var ai2 = new AISystem();
            ai1.RegisterDefinition(new EnemyDefinition("test", "Test", 10f, 1f, AiType.Patrol));
            ai1.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(ai1.TrySpawn("test", new Vector3(10, 0, 10)));
            Assert.False(ai2.TrySpawn("test", new Vector3(10, 0, 10))); // ai2 doesn't have the definition
        }

        [Fact]
        public void Instances_AreIsolated_State()
        {
            var ai1 = new AISystem();
            var ai2 = new AISystem();
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
            var ai1 = new AISystem(config1);
            var ai2 = new AISystem(config2);
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
            var ai1 = new AISystem(randomProvider: fixedRandom);
            var ai2 = new AISystem(randomProvider: fixedRandom);
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
    }
}