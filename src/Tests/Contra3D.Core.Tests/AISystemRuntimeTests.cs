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
        private static void RegisterDefs()
        {
            AISystem.RegisterDefinition(new EnemyDefinition("grunt", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, attackRange: 3f, alertThreshold: 60f));
            AISystem.RegisterDefinition(new EnemyDefinition("hunter", "Hunter", 18f, 5f, AiType.Chase,
                visionRange: 20f, attackRange: 2f));
            AISystem.RegisterDefinition(new EnemyDefinition("sniper", "Sniper", 30f, 0f, AiType.Sniper,
                visionRange: 35f, attackRange: 30f));
            AISystem.RegisterDefinition(new EnemyDefinition("rusher", "Rusher", 12f, 6f, AiType.Rusher,
                visionRange: 12f, attackRange: 1f));
        }

        // ---- RegisterDefinition ----

        [Fact]
        public void RegisterDefinition_Null_ThrowsArgumentNullException()
        {
            AISystem.TestReset();
            Assert.Throws<ArgumentNullException>(() => AISystem.RegisterDefinition(null!));
        }

        [Fact]
        public void RegisterDefinition_AllowsSpawn()
        {
            AISystem.TestReset();
            AISystem.RegisterDefinition(new EnemyDefinition("test", "Test", 10f, 1f, AiType.Patrol));
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(AISystem.TrySpawn("test", new Vector3(10, 0, 10)));
        }

        // ---- TrySpawn ----

        [Fact]
        public void TrySpawn_UnknownEnemy_ReturnsFalse()
        {
            AISystem.TestReset();
            RegisterDefs();
            Assert.False(AISystem.TrySpawn("nonexistent", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_TooCloseToPlayer_ReturnsFalse()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            Assert.False(AISystem.TrySpawn("grunt", Vector3.Zero));
        }

        [Fact]
        public void TrySpawn_NormalCapEnqueues()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 12; i++)
                AISystem.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            Assert.Equal(12, AISystem.ActiveCount);
            Assert.False(AISystem.TrySpawn("grunt", new Vector3(50, 0, 50)));
            Assert.Equal(12, AISystem.ActiveCount);
        }

        [Fact]
        public void TrySpawn_RusherCapEnqueues()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 4; i++)
                AISystem.TrySpawn("rusher", new Vector3(10 + i, 0, 10));
            Assert.Equal(4, AISystem.RusherCount);
            Assert.False(AISystem.TrySpawn("rusher", new Vector3(20, 0, 20)));
            Assert.Equal(4, AISystem.RusherCount);
        }

        [Fact]
        public void TrySpawn_Successful_SetsInitialState()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            bool spawned = AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(spawned);
            var state = AISystem.GetStates()["grunt"];
            Assert.Equal(AiState.Idle, state.State);
            Assert.Equal(24f, state.Health);
        }

        [Fact]
        public void TrySpawn_AcceptableDistance_ReturnsTrue()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            Assert.True(AISystem.TrySpawn("grunt", new Vector3(10, 0, 0)));
        }

        [Fact]
        public void TrySpawn_Boundary_5m_IsAccepted()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            Assert.True(AISystem.TrySpawn("grunt", new Vector3(5f, 0, 0)));
        }

        [Fact]
        public void TrySpawn_JustInsideBoundary_4_9m_IsRejected()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            Assert.False(AISystem.TrySpawn("grunt", new Vector3(4.9f, 0, 0)));
        }

        // ---- OnEnemyDead ----

        [Fact]
        public void OnEnemyDead_RemovesEntity()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(AISystem.GetStates().ContainsKey("grunt"));
            AISystem.OnEnemyDead("grunt");
            Assert.False(AISystem.GetStates().ContainsKey("grunt"));
        }

        [Fact]
        public void OnEnemyDead_UnknownId_IsNoOp()
        {
            AISystem.TestReset();
            AISystem.OnEnemyDead("nobody");
        }

        [Fact]
        public void OnEnemyDead_ReleasesQueuedSpawn()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            for (int i = 0; i < 12; i++)
                AISystem.TrySpawn("grunt", new Vector3(10 + i, 0, 10));
            AISystem.TrySpawn("grunt", new Vector3(50, 0, 50));
            Assert.Equal(12, AISystem.ActiveCount);
            var ids = new List<string>(AISystem.GetStates().Keys);
            AISystem.OnEnemyDead(ids[0]);
            Assert.Equal(12, AISystem.ActiveCount);
        }

        // ---- Update ----

        [Fact]
        public void Update_Patrol_GainsVigilanceNearPlayer()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(10, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(5, 0, 5));
            AISystem.Update(2f);
            var state = AISystem.GetStates()["grunt"];
            Assert.True(state.Vigilance >= 30, $"vigilance={state.Vigilance}");
        }

        [Fact]
        public void Update_Chase_EntersChaseOrAlert()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(10, 0, 0));
            AISystem.TrySpawn("hunter", new Vector3(5, 0, 5));
            AISystem.Update(1f);
            var state = AISystem.GetStates()["hunter"];
            Assert.True(state.State == AiState.Chase || state.State == AiState.Alert, $"got {state.State}");
        }

        [Fact]
        public void Update_IgnoresInvalidDt()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            int countBefore = AISystem.ActiveCount;
            AISystem.Update(0f);
            AISystem.Update(float.NaN);
            AISystem.Update(float.PositiveInfinity);
            Assert.Equal(countBefore, AISystem.ActiveCount);
        }

        [Fact]
        public void Update_Rusher_ReachesPlayer_Dies()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            AISystem.TrySpawn("rusher", new Vector3(1, 0, 0));
            AISystem.Update(1f);
            Assert.False(AISystem.GetStates().ContainsKey("rusher"));
        }

        [Fact]
        public void Update_Patrol_VigilanceDecaysOutOfSight()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(5, 0, 0));
            AISystem.TrySpawn("grunt", Vector3.Zero);
            AISystem.Update(4f);
            var preState = AISystem.GetStates()["grunt"];
            Assert.Equal(AiState.Alert, preState.State);

            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.Update(8f);
            var postState = AISystem.GetStates()["grunt"];
            Assert.True(postState.Vigilance < 60f, $"vigilance={postState.Vigilance:F2}");
        }

        [Fact]
        public void Update_Sniper_EntersAimState()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(32, 0, 0));
            AISystem.TrySpawn("sniper", Vector3.Zero);
            AISystem.Update(1f);
            var state = AISystem.GetStates()["sniper"];
            Assert.Equal(AiState.Aim, state.State);
        }

        // ---- GetCommand ----

        [Fact]
        public void GetCommand_UnknownEnemy_ReturnsIdle()
        {
            AISystem.TestReset();
            var cmd = AISystem.GetCommand("nobody");
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void GetCommand_IdleState_ReturnsIdle()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            var cmd = AISystem.GetCommand("grunt");
            Assert.Equal(AICommand.Idle.MoveIntent, cmd.MoveIntent);
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void GetCommand_OutOfRange_ReturnsIdle()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(5, 0, 5));
            var cmd = AISystem.GetCommand("grunt");
            Assert.False(cmd.FireRequest);
        }

        // ---- SetPlayerPosition ----

        [Fact]
        public void SetPlayerPosition_AffectsSpawnDistanceCheck()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(Vector3.Zero);
            Assert.False(AISystem.TrySpawn("grunt", new Vector3(2, 0, 0)));
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            Assert.True(AISystem.TrySpawn("grunt", new Vector3(2, 0, 2)));
        }

        // ---- GetStates ----

        [Fact]
        public void GetStates_ReturnsCurrentStates()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            AISystem.TrySpawn("hunter", new Vector3(11, 0, 11));
            var states = AISystem.GetStates();
            Assert.True(states.ContainsKey("grunt"));
            Assert.True(states.ContainsKey("hunter"));
        }

        [Fact]
        public void GetStates_EmptyWhenNoSpawn()
        {
            AISystem.TestReset();
            RegisterDefs();
            var states = AISystem.GetStates();
            Assert.Empty(states);
        }

        [Fact]
        public void GetStates_RemovesDeadEnemy()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.True(AISystem.GetStates().ContainsKey("grunt"));
            AISystem.OnEnemyDead("grunt");
            Assert.False(AISystem.GetStates().ContainsKey("grunt"));
        }

        // ---- ActiveCount / RusherCount ----

        [Fact]
        public void ActiveCount_IncreasesWithSpawn()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = AISystem.ActiveCount;
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, AISystem.ActiveCount);
        }

        [Fact]
        public void ActiveCount_DecreasesWithDead()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("grunt", new Vector3(10, 0, 10));
            int before = AISystem.ActiveCount;
            AISystem.OnEnemyDead("grunt");
            Assert.Equal(before - 1, AISystem.ActiveCount);
        }

        [Fact]
        public void RusherCount_IncreasesWithRusherSpawn()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            int before = AISystem.RusherCount;
            AISystem.TrySpawn("rusher", new Vector3(10, 0, 10));
            Assert.Equal(before + 1, AISystem.RusherCount);
        }

        [Fact]
        public void RusherCount_DecreasesWithRusherDead()
        {
            AISystem.TestReset();
            RegisterDefs();
            AISystem.SetPlayerPosition(new Vector3(100, 0, 0));
            AISystem.TrySpawn("rusher", new Vector3(10, 0, 10));
            int before = AISystem.RusherCount;
            AISystem.OnEnemyDead("rusher");
            Assert.Equal(before - 1, AISystem.RusherCount);
        }
    }
}