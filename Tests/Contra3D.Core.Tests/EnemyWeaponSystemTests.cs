using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class EnemyWeaponSystemTests
    {
        private static Dictionary<string, WeaponDefinition> MakeWeapons()
        {
            var w = new Dictionary<string, WeaponDefinition>();
            w["pistol"] = new WeaponDefinition("pistol", "Pistol", WeaponType.Hitscan, 10f, 4f, 999, 0f, 5f);
            w["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 15f, 8f, 999, 0f, 2f);
            w["shotgun"] = new WeaponDefinition("shotgun", "Shotgun", WeaponType.Projectile, 8f, 2f, 999, 10f, 10f);
            w["laser"] = new WeaponDefinition("laser", "Laser", WeaponType.Hitscan, 25f, 3f, 999, 0f, 0f);
            return w;
        }

        private static EnemyWeaponSystem MakeSystem(Dictionary<string, WeaponDefinition> weapons = null, IRandomProvider random = null)
        {
            return new EnemyWeaponSystem(weapons ?? MakeWeapons(), random);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullWeapons_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new EnemyWeaponSystem(null, new DefaultRandomProvider()));
        }

        [Fact]
        public void Constructor_InitializesEmptyState()
        {
            var sys = MakeSystem();
            Assert.Empty(sys.PendingRequests);
        }

        [Fact]
        public void Constructor_UsesDefaultRandomProvider_WhenNull()
        {
            var sys = new EnemyWeaponSystem(MakeWeapons(), null);
            // Should not throw
            Assert.NotNull(sys);
        }

        #endregion

        #region RegisterEnemy Tests

        [Fact]
        public void RegisterEnemy_SuccessfulRegistration()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void RegisterEnemy_WithUnknownWeapon_ThrowsArgumentException()
        {
            var sys = MakeSystem();
            Assert.Throws<ArgumentException>(() => sys.RegisterEnemy("grunt1", "unknown_weapon", 5f));
        }

        [Fact]
        public void RegisterEnemy_FireIntervalClampedToMin()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            // Very high fire rate (100/sec) would give 0.01s interval, but min is 0.08f
            weapons["fast"] = new WeaponDefinition("fast", "Fast", WeaponType.Hitscan, 5f, 100f, 999, 0f, 0f);
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("fast_enemy", "fast", 100f);
            // After registering, should be ready immediately
            Assert.True(sys.IsWeaponReady("fast_enemy"));
        }

        [Fact]
        public void RegisterEnemy_UsesWeaponDefinitionMinFireInterval()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["slow"] = new WeaponDefinition("slow", "Slow", WeaponType.Hitscan, 10f, 1f, 999, 0f, 2f); // minFireInterval=2f
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("slow_enemy", "slow", 0.5f); // requested 0.5f but should clamp to 2f
            Assert.True(sys.IsWeaponReady("slow_enemy"));
        }

        [Fact]
        public void RegisterEnemy_DuplicateRegistration_Overwrites()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.RegisterEnemy("grunt1", "rifle", 8f); // same enemy, different weapon
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        #endregion

        #region Update (Cooldown Timer) Tests

        [Fact]
        public void Update_DecrementsCooldownTimer()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f); // 4 fire rate -> 0.25s interval
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            Assert.False(sys.IsWeaponReady("grunt1")); // should be on cooldown

            sys.Update(0.1f);
            Assert.False(sys.IsWeaponReady("grunt1")); // still on cooldown

            sys.Update(0.2f);
            Assert.True(sys.IsWeaponReady("grunt1")); // now ready (0.1 + 0.2 = 0.3 > 0.25)
        }

        [Fact]
        public void Update_MultipleEnemies_AllTimersAdvance()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("e1", "pistol", 2f); // 0.5s interval
            sys.RegisterEnemy("e2", "rifle", 8f); // 0.125s interval

            sys.TryFire("e1", Vector3.Zero, Vector3.UnitX);
            sys.TryFire("e2", Vector3.Zero, Vector3.UnitX);

            sys.Update(0.2f);
            Assert.False(sys.IsWeaponReady("e1")); // 0.2 < 0.5
            Assert.True(sys.IsWeaponReady("e2")); // 0.2 > 0.125
        }

        [Fact]
        public void Update_NegativeDt_ThrowsArgumentOutOfRangeException()
        {
            var sys = MakeSystem();
            Assert.Throws<ArgumentOutOfRangeException>(() => sys.Update(-0.1f));
        }

        [Fact]
        public void Update_ZeroDt_ThrowsArgumentOutOfRangeException()
        {
            var sys = MakeSystem();
            Assert.Throws<ArgumentOutOfRangeException>(() => sys.Update(0f));
        }

        [Fact]
        public void Update_NaNt_ThrowsArgumentOutOfRangeException()
        {
            var sys = MakeSystem();
            Assert.Throws<ArgumentOutOfRangeException>(() => sys.Update(float.NaN));
        }

        [Fact]
        public void Update_InfiniteDt_ThrowsArgumentOutOfRangeException()
        {
            var sys = MakeSystem();
            Assert.Throws<ArgumentOutOfRangeException>(() => sys.Update(float.PositiveInfinity));
        }

        [Fact]
        public void Update_CooldownNeverGoesBelowZero()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);

            sys.Update(10f); // large dt
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        #endregion

        #region IsWeaponReady Tests

        [Fact]
        public void IsWeaponReady_UnknownEnemy_ReturnsFalse()
        {
            var sys = MakeSystem();
            Assert.False(sys.IsWeaponReady("nonexistent"));
        }

        [Fact]
        public void IsWeaponReady_AfterRegistration_ReturnsTrue()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void IsWeaponReady_DuringCooldown_ReturnsFalse()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            Assert.False(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void IsWeaponReady_AfterCooldownExpires_ReturnsTrue()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f); // 0.25s interval
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            
            sys.Update(0.3f);
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        #endregion

        #region TryFire Tests

        [Fact]
        public void TryFire_UnknownEnemy_ReturnsFalse()
        {
            var sys = MakeSystem();
            Assert.False(sys.TryFire("nonexistent", Vector3.Zero, Vector3.UnitX));
        }

        [Fact]
        public void TryFire_DuringCooldown_ReturnsFalse()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            Assert.False(sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX));
        }

        [Fact]
        public void TryFire_SuccessfulFire_AddsRequest()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            
            bool result = sys.TryFire("grunt1", new Vector3(0, 1, 0), Vector3.UnitX);
            
            Assert.True(result);
            Assert.Single(sys.PendingRequests);
        }

        [Fact]
        public void TryFire_SetsCooldownTimer()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f); // 0.25s interval
            
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            
            Assert.False(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void TryFire_RequestContainsCorrectData()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["precise_rifle"] = new WeaponDefinition("precise_rifle", "PreciseRifle", WeaponType.Hitscan, 15f, 8f, 999, 0f, 0f);
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("grunt1", "precise_rifle", 8f);

            var origin = new Vector3(10, 5, 0);
            var direction = new Vector3(0, 0, -1);
            sys.TryFire("grunt1", origin, direction);

            var request = sys.PendingRequests[0];
            Assert.Equal("grunt1", request.EnemyId);
            Assert.Equal("precise_rifle", request.WeaponId);
            Assert.Equal(origin, request.Origin);
            Assert.Equal(direction, request.Direction);
        }

        [Fact]
        public void TryFire_MultipleFires_AddsMultipleRequests()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            sys.Update(1f); // let cooldown expire
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            
            Assert.Equal(2, sys.PendingRequests.Count);
        }

        [Fact]
        public void TryFire_WithSpread_AppliesSpreadToDirection()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["spread_gun"] = new WeaponDefinition("spread_gun", "SpreadGun", WeaponType.Hitscan, 10f, 5f, 999, 15f, 15f);
            
            var random = new DeterministicRandomProvider(seed: 42);
            var sys = MakeSystem(weapons, random);
            sys.RegisterEnemy("grunt1", "spread_gun", 5f);
            
            var originalDirection = Vector3.UnitX;
            sys.TryFire("grunt1", Vector3.Zero, originalDirection);
            
            var request = sys.PendingRequests[0];
            // Direction should be modified by spread (not exactly original)
            Assert.NotEqual(originalDirection, request.Direction);
            // But should still be roughly forward
            Assert.True(request.Direction.X > 0);
        }

        [Fact]
        public void TryFire_WithoutSpread_KeepsOriginalDirection()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["precision"] = new WeaponDefinition("precision", "Precision", WeaponType.Hitscan, 10f, 5f, 999, 0f, 0f);
            
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("grunt1", "precision", 5f);
            
            var originalDirection = Vector3.UnitZ;
            sys.TryFire("grunt1", Vector3.Zero, originalDirection);
            
            var request = sys.PendingRequests[0];
            Assert.Equal(originalDirection, request.Direction);
        }

        #endregion

        #region ConsumeRequests Tests

        [Fact]
        public void ConsumeRequests_ReturnsAllPendingRequests()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("e1", "pistol", 4f);
            sys.RegisterEnemy("e2", "rifle", 8f);
            
            sys.TryFire("e1", Vector3.Zero, Vector3.UnitX);
            sys.TryFire("e2", Vector3.Zero, Vector3.UnitY);
            
            var requests = sys.ConsumeRequests();
            
            Assert.Equal(2, requests.Count);
        }

        [Fact]
        public void ConsumeRequests_ClearsPendingQueue()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            
            sys.ConsumeRequests();
            
            Assert.Empty(sys.PendingRequests);
        }

        [Fact]
        public void ConsumeRequests_WithNoPending_ReturnsEmptyList()
        {
            var sys = MakeSystem();
            var requests = sys.ConsumeRequests();
            
            Assert.Empty(requests);
        }

        [Fact]
        public void ConsumeRequests_ReturnsIndependentCopy()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);
            
            var requests = sys.ConsumeRequests();
            
            // Clearing the returned list shouldn't affect the system
            requests.Clear();
            Assert.Empty(sys.PendingRequests); // already empty from consume
        }

        #endregion

        #region UnregisterEnemy Tests

        [Fact]
        public void UnregisterEnemy_RemovesEnemyState()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.UnregisterEnemy("grunt1");
            
            Assert.False(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void UnregisterEnemy_AllowsReRegistration()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.UnregisterEnemy("grunt1");
            sys.RegisterEnemy("grunt1", "rifle", 8f);
            
            Assert.True(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void UnregisterEnemy_NonExistentEnemy_NoThrow()
        {
            var sys = MakeSystem();
            // Should not throw
            sys.UnregisterEnemy("nonexistent");
        }

        #endregion

        #region Cooldown/Fire/Request Integration Scenarios

        [Fact]
        public void Scenario_CooldownFireCycle()
        {
            // Simulate a complete cooldown -> fire -> cooldown cycle
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f); // 4 fire rate = 0.25s interval

            // Initial state: ready
            Assert.True(sys.IsWeaponReady("grunt1"));
            Assert.Empty(sys.PendingRequests);

            // Fire!
            bool fired = sys.TryFire("grunt1", new Vector3(0, 2, 0), Vector3.Forward);
            Assert.True(fired);
            Assert.Single(sys.PendingRequests);
            Assert.False(sys.IsWeaponReady("grunt1"));

            // Consume the request
            var requests = sys.ConsumeRequests();
            Assert.Single(requests);
            Assert.Equal("grunt1", requests[0].EnemyId);
            Assert.Equal("pistol", requests[0].WeaponId);
            Assert.Empty(sys.PendingRequests);

            // Wait for cooldown
            sys.Update(0.3f);
            Assert.True(sys.IsWeaponReady("grunt1"));

            // Can fire again
            fired = sys.TryFire("grunt1", new Vector3(0, 2, 0), Vector3.Forward);
            Assert.True(fired);
        }

        [Fact]
        public void Scenario_MultipleEnemiesSimultaneousFire()
        {
            // Multiple enemies firing in the same frame
            var sys = MakeSystem();
            sys.RegisterEnemy("e1", "pistol", 4f);
            sys.RegisterEnemy("e2", "rifle", 8f);
            sys.RegisterEnemy("e3", "shotgun", 2f);

            sys.TryFire("e1", new Vector3(0, 0, 0), Vector3.UnitX);
            sys.TryFire("e2", new Vector3(1, 0, 0), Vector3.UnitY);
            sys.TryFire("e3", new Vector3(2, 0, 0), Vector3.UnitZ);

            var requests = sys.ConsumeRequests();
            Assert.Equal(3, requests.Count);

            var enemyIds = new HashSet<string>();
            foreach (var req in requests)
            {
                enemyIds.Add(req.EnemyId);
            }
            Assert.Contains("e1", enemyIds);
            Assert.Contains("e2", enemyIds);
            Assert.Contains("e3", enemyIds);
        }

        [Fact]
        public void Scenario_FireThenConsumeThenFireAgain()
        {
            // Verify the full pipeline: fire, consume, wait, fire again
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "laser", 3f); // 3 fire rate = ~0.33s interval

            // First burst
            sys.TryFire("grunt1", Vector3.Zero, Vector3.Forward);
            var requests1 = sys.ConsumeRequests();
            Assert.Single(requests1);

            // Should be on cooldown
            Assert.False(sys.IsWeaponReady("grunt1"));

            // Wait partial cooldown
            sys.Update(0.2f);
            Assert.False(sys.IsWeaponReady("grunt1"));

            // Wait full cooldown
            sys.Update(0.2f);
            Assert.True(sys.IsWeaponReady("grunt1"));

            // Second burst
            sys.TryFire("grunt1", Vector3.Zero, Vector3.Forward);
            var requests2 = sys.ConsumeRequests();
            Assert.Single(requests2);
        }

        [Fact]
        public void Scenario_EnemyDeathRemovesWeapon()
        {
            // When enemy dies, weapon should be unregistered
            var sys = MakeSystem();
            sys.RegisterEnemy("grunt1", "pistol", 4f);
            sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX);

            // Enemy dies
            sys.UnregisterEnemy("grunt1");

            // Cannot fire anymore
            Assert.False(sys.TryFire("grunt1", Vector3.Zero, Vector3.UnitX));
            Assert.False(sys.IsWeaponReady("grunt1"));
        }

        [Fact]
        public void Scenario_RapidFireSequence()
        {
            // Test rapid fire with small dt increments
            var sys = MakeSystem();
            sys.RegisterEnemy("machinegunner", "rifle", 8f); // 8 fire rate = 0.125s interval

            for (int i = 0; i < 10; i++)
            {
                sys.Update(0.1f);
                if (sys.IsWeaponReady("machinegunner"))
                {
                    sys.TryFire("machinegunner", Vector3.Zero, Vector3.UnitX);
                }
            }

            // Should have accumulated some requests
            var requests = sys.ConsumeRequests();
            Assert.True(requests.Count >= 5); // at least 5 shots fired
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void EdgeCase_VeryHighFireRate_Clamped()
        {
            // Fire rate so high that interval would be tiny, should clamp to min
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["insane"] = new WeaponDefinition("insane", "Insane", WeaponType.Hitscan, 1f, 1000f, 999, 0f, 0f);
            
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("e1", "insane", 1000f);
            
            sys.TryFire("e1", Vector3.Zero, Vector3.UnitX);
            
            // Should have cooldown of at least MinFireIntervalS
            Assert.False(sys.IsWeaponReady("e1"));
            
            // Small dt shouldn't make it ready
            sys.Update(0.05f);
            Assert.False(sys.IsWeaponReady("e1"));
            
            // Min interval dt should make it ready
            sys.Update(0.08f);
            Assert.True(sys.IsWeaponReady("e1"));
        }

        [Fact]
        public void EdgeCase_ZeroSpread_NoDirectionChange()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["precise"] = new WeaponDefinition("precise", "Precise", WeaponType.Hitscan, 10f, 5f, 999, 0f, 0f);
            
            var sys = MakeSystem(weapons);
            sys.RegisterEnemy("e1", "precise", 5f);
            
            var direction = new Vector3(1, 1, 1);
            sys.TryFire("e1", Vector3.Zero, direction);
            
            var request = sys.PendingRequests[0];
            Assert.Equal(direction, request.Direction);
        }

        [Fact]
        public void EdgeCase_PendingRequestsIsReadOnly()
        {
            var sys = MakeSystem();
            sys.RegisterEnemy("e1", "pistol", 4f);
            
            // Should be able to read but not modify via the interface
            var readOnly = sys.PendingRequests;
            Assert.NotNull(readOnly);
            Assert.Empty(readOnly);
        }

        #endregion
    }
}
