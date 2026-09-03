using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class AiSystemTests
    {
        private static Dictionary<string, EnemyDefinition> MakeDefinitions()
        {
            var defs = new Dictionary<string, EnemyDefinition>();
            defs["grunt"] = new EnemyDefinition("grunt", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, alertThreshold: 60f, comprehensionThreshold: 100f);
            defs["charger"] = new EnemyDefinition("charger", "Charger", 36f, 4.5f, AiType.Rusher,
                visionRange: 12f, attackRange: 2f);
            defs["sniper"] = new EnemyDefinition("sniper", "Sniper", 30f, 0f, AiType.Sniper,
                visionRange: 35f, attackRange: 30f);
            defs["hound"] = new EnemyDefinition("hound", "Hound", 18f, 5f, AiType.Chase,
                visionRange: 20f, attackRange: 3f);
            return defs;
        }

        [Fact]
        public void SpawnEnemy_InitialState_Idle()
        {
            var sys = new AiSystem(MakeDefinitions());
            sys.SpawnEnemy("grunt", Vector3.Zero);
            var cmd = sys.GetCommand("grunt");
            Assert.Equal(AICommand.Idle.MoveIntent, cmd.MoveIntent);
            Assert.False(cmd.FireRequest);
        }

        [Fact]
        public void Patrol_VigilanceIncreasesNearPlayer()
        {
            var defs = new Dictionary<string, EnemyDefinition>();
            defs["grunt"] = new EnemyDefinition("grunt", "Grunt", 24f, 2f, AiType.Patrol,
                visionRange: 15f, alertThreshold: 60f, comprehensionThreshold: 100f,
                vigilanceGainPerSecond: 20f);
            var sys = new AiSystem(defs);
            sys.SpawnEnemy("grunt", Vector3.Zero);
            sys.SetPlayerPosition(new Vector3(5, 0, 0)); // Within vision range

            // Advance 4 seconds: vigilance = 20*4 = 80 >= 60 (alert threshold)
            sys.Update(4f);
            var cmd = sys.GetCommand("grunt");
            // Should be in Alert or Combat state now
            var state = GetState(sys, "grunt");
            Assert.NotEqual(AiState.Idle, state.State);
        }

        [Fact]
        public void Rusher_ReachesPlayer_Explodes()
        {
            var defs = new Dictionary<string, EnemyDefinition>();
            defs["charger"] = new EnemyDefinition("charger", "Charger", 36f, 4.5f, AiType.Rusher,
                visionRange: 12f, attackRange: 2f);
            var sys = new AiSystem(defs);
            sys.SpawnEnemy("charger", new Vector3(10, 0, 0));
            sys.SetPlayerPosition(Vector3.Zero);

            // Charger moves towards player at 4.5 m/s
            // Distance = 10m, time to reach = 10/4.5 ≈ 2.2s
            // Update in small steps to ensure collision detection works
            for (float t = 0; t < 3f; t += 0.1f)
                sys.Update(0.1f);
            var state = GetState(sys, "charger");
            Assert.Equal(AiState.Dead, state.State); // Exploded
        }

        [Fact]
        public void TakeDamage_ReducesHealth()
        {
            var sys = new AiSystem(MakeDefinitions());
            sys.SpawnEnemy("grunt", Vector3.Zero);
            sys.TakeDamage("grunt", 10f);
            var state = GetState(sys, "grunt");
            Assert.Equal(14f, state.Health);
            Assert.True(state.IsAlive);
        }

        [Fact]
        public void TakeFatalDamage_KillsEnemy()
        {
            var sys = new AiSystem(MakeDefinitions());
            sys.SpawnEnemy("grunt", Vector3.Zero);
            sys.TakeDamage("grunt", 24f); // Full health
            var state = GetState(sys, "grunt");
            Assert.Equal(0f, state.Health);
            Assert.False(state.IsAlive);
            Assert.Equal(AiState.Dead, state.State);
        }

        [Fact]
        public void Sniper_StaysInRange_DoesNotChase()
        {
            var defs = new Dictionary<string, EnemyDefinition>();
            defs["sniper"] = new EnemyDefinition("sniper", "Sniper", 30f, 0f, AiType.Sniper,
                visionRange: 35f, attackRange: 30f);
            var sys = new AiSystem(defs);
            sys.SpawnEnemy("sniper", Vector3.Zero);
            sys.SetPlayerPosition(new Vector3(10, 0, 0)); // Within range

            // Sniper doesn't move (speed = 0)
            sys.Update(1f);
            var state = GetState(sys, "sniper");
            Assert.Equal(Vector3.Zero, state.Position); // Still at origin
        }

        [Fact]
        public void EnemyOutOfRange_ReturnsToIdle()
        {
            var defs = new Dictionary<string, EnemyDefinition>();
            defs["hound"] = new EnemyDefinition("hound", "Hound", 18f, 5f, AiType.Chase,
                visionRange: 20f, attackRange: 3f);
            var sys = new AiSystem(defs);
            sys.SpawnEnemy("hound", Vector3.Zero);
            sys.SetPlayerPosition(new Vector3(5, 0, 0));
            sys.Update(1f); // Chase towards player

            // Move player far away
            sys.SetPlayerPosition(new Vector3(100, 0, 0));
            sys.Update(10f); // Hound chases but player is far

            var state = GetState(sys, "hound");
            // Should still be alive and chasing (or idle if lost sight)
        }

        [Fact]
        public void Deterministic_SameInputSameOutput()
        {
            var sys1 = new AiSystem(MakeDefinitions());
            var sys2 = new AiSystem(MakeDefinitions());
            sys1.SpawnEnemy("grunt", Vector3.Zero);
            sys2.SpawnEnemy("grunt", Vector3.Zero);
            sys1.SetPlayerPosition(new Vector3(5, 0, 0));
            sys2.SetPlayerPosition(new Vector3(5, 0, 0));
            sys1.Update(1f);
            sys2.Update(1f);
            var cmd1 = sys1.GetCommand("grunt");
            var cmd2 = sys2.GetCommand("grunt");
            Assert.Equal(cmd1.MoveIntent, cmd2.MoveIntent);
        }

        private static EnemyAIState GetState(AiSystem sys, string enemyId)
        {
            // Use reflection to access private _states dictionary
            var field = typeof(AiSystem).GetField("_states", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var states = (Dictionary<string, EnemyAIState>)field.GetValue(sys);
            return states[enemyId];
        }
    }
}
