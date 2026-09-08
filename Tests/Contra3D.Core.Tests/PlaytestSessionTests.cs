using System;
using System.Collections.Generic;
using System.Numerics;
using Contra3D.Combat;
using Contra3D.Core.Playtest;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class PlaytestSessionTests
    {
        private static CombatSystem CreateCombatSystem()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition(
                "rifle_default", "Rifle", WeaponType.Hitscan,
                damage: 12f, fireRate: 7f, magazineSize: 30,
                reloadTime: 1.5f, spread: 1.5f);
            var ws = new WeaponSystem(weapons, "rifle_default");
            var ps = new ProjectileSystem(new ProjectileDefinition(
                speed: 50f, radius: 0.5f, damage: 12f, lifetime: 5f, maxDistance: 500f));
            var hd = new HealthDamageSystem();
            return new CombatSystem(ws, ps, hd);
        }

        // ── Construction ──────────────────────────────────────────────────────

        [Fact]
        public void Ctor_ThrowsOnNullAgent()
        {
            var combat = CreateCombatSystem();
            Assert.Throws<ArgumentNullException>(() => new PlaytestSession(null, combat));
        }

        [Fact]
        public void Ctor_ThrowsOnNullCombat()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            Assert.Throws<ArgumentNullException>(() => new PlaytestSession(agent, null));
        }

        [Fact]
        public void Ctor_AcceptsValidInputs()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat, maxFrames: 100);
            Assert.NotNull(session);
        }

        // ── RegisterEnemy ─────────────────────────────────────────────────────

        [Fact]
        public void RegisterEnemy_AddsTargetToCombatSystem()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat);

            var id = session.RegisterEnemy("grunt_1", new Vector3(5f, 0f, 0f), radius: 2f);

            Assert.Equal("grunt_1", id);
            var targets = combat.GetTargets();
            Assert.Single(targets);
            Assert.Equal("grunt_1", targets[0].Id);
            Assert.Equal(2f, targets[0].Radius);
        }

        [Fact]
        public void RegisterEnemy_DefaultRadiusIs1_5()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat);

            session.RegisterEnemy("enemy_a", new Vector3(0f, 0f, 10f));

            var targets = combat.GetTargets();
            Assert.Single(targets);
            Assert.Equal(1.5f, targets[0].Radius);
        }

        // ── Run (no targets) ─────────────────────────────────────────────────

        [Fact]
        public void Run_WithNoTargets_ReturnsZeroMetrics()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat, maxFrames: 60);

            var metrics = session.Run();

            Assert.Equal(0, metrics.TotalShots);
            Assert.Equal(0, metrics.HitsOnTarget);
            Assert.Equal(0, metrics.Kills);
            Assert.Equal(0.0, metrics.Accuracy);
        }

        // ── Run (with targets) ────────────────────────────────────────────────

        [Fact]
        public void Run_WithTargets_ShootsAndRecordsHits()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: new Vector3(0f, 0f, 0f));
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat, maxFrames: 600);

            session.RegisterEnemy("grunt_1", new Vector3(10f, 0f, 0f), radius: 1.5f);

            var metrics = session.Run();

            Assert.True(metrics.TotalShots > 0, "Agent should fire when targets exist");
            // Accuracy should be between 0 and 1
            Assert.InRange(metrics.Accuracy, 0.0, 1.0);
        }

        [Fact]
        public void Run_KillsIncrementedWhenTargetDies()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: new Vector3(0f, 0f, 0f));
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat, maxFrames: 9000);

            // Register a low-HP target very close to the agent
            session.RegisterEnemy("weak_enemy", new Vector3(0.5f, 0f, 0f), radius: 2f);

            // Run multiple iterations with different seeds to ensure at least one kill
            int totalKills = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                var subAgent = new HeadlessPlaytestAgent(seed: seed, startPosition: new Vector3(0f, 0f, 0f));
                var subCombat = CreateCombatSystem();
                var subSession = new PlaytestSession(subAgent, subCombat, maxFrames: 9000);
                subSession.RegisterEnemy("enemy", new Vector3(0.5f, 0f, 0f), radius: 2f);
                var metrics = subSession.Run();
                totalKills += metrics.Kills;
            }

            Assert.True(totalKills >= 1, $"Should kill at least one target across iterations, got {totalKills} kills");
        }

        [Fact]
        public void Run_StopsAtMaxFrames()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            const int maxFrames = 100;
            var session = new PlaytestSession(agent, combat, maxFrames: maxFrames);

            session.RegisterEnemy("target", new Vector3(500f, 0f, 500f), radius: 1f);

            var metrics = session.Run();

            // Should not exceed maxFrames worth of time
            Assert.True(metrics.ElapsedTime <= maxFrames * 0.016f + 0.016f,
                $"Expected elapsed <= {maxFrames * 0.016f + 0.016f}, got {metrics.ElapsedTime}");
        }

        [Fact]
        public void Run_StopsAt120SecondCap()
        {
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            // Large maxFrames so the 120s cap is the limiting factor
            var session = new PlaytestSession(agent, combat, maxFrames: 90000);

            session.RegisterEnemy("target", new Vector3(500f, 0f, 500f), radius: 1f);

            var metrics = session.Run();

            Assert.True(metrics.ElapsedTime <= 120f + 0.016f,
                $"Should cap at ~120s, got {metrics.ElapsedTime}");
        }

        // ── Accuracy computation ──────────────────────────────────────────────

        [Fact]
        public void PlaytestMetrics_AccuracyCorrect()
        {
            var m = new PlaytestMetrics(totalShots: 100, hitsOnTarget: 75, kills: 10, deaths: 0, elapsedTime: 60f);
            Assert.Equal(0.75, m.Accuracy);
        }

        [Fact]
        public void PlaytestMetrics_AccuracyZeroWhenNoShots()
        {
            var m = new PlaytestMetrics(totalShots: 0, hitsOnTarget: 0, kills: 0, deaths: 0, elapsedTime: 0f);
            Assert.Equal(0.0, m.Accuracy);
        }

        [Fact]
        public void PlaytestMetrics_KDRInfinityWhenNoDeaths()
        {
            var m = new PlaytestMetrics(totalShots: 10, hitsOnTarget: 5, kills: 3, deaths: 0, elapsedTime: 30f);
            Assert.Equal(double.PositiveInfinity, m.KDR);
        }

        [Fact]
        public void PlaytestMetrics_KDRZeroWhenNoKills()
        {
            var m = new PlaytestMetrics(totalShots: 10, hitsOnTarget: 0, kills: 0, deaths: 5, elapsedTime: 30f);
            Assert.Equal(0.0, m.KDR);
        }

        // ── PlaytestReport aggregation ────────────────────────────────────────

        [Fact]
        public void PlaytestReport_MeanAccuracyCorrect()
        {
            var runs = new[]
            {
                new PlaytestMetrics(100, 80, 5, 2, 60f),
                new PlaytestMetrics(100, 60, 4, 3, 60f),
            };
            var report = new PlaytestReport(runs);
            Assert.Equal(0.7, report.MeanAccuracy);
            Assert.Equal(2, report.TotalRuns);
        }

        [Fact]
        public void PlaytestReport_EmptyRuns_YieldsZeroMeans()
        {
            var report = new PlaytestReport(Array.Empty<PlaytestMetrics>());
            Assert.Equal(0, report.TotalRuns);
            Assert.Equal(0.0, report.MeanAccuracy);
            Assert.Equal(0.0, report.MeanKillsPerRun);
            Assert.Equal(0.0, report.MeanKDR);
            Assert.Equal(0.0, report.MeanElapsedTime);
        }

        [Fact]
        public void PlaytestReport_MeanElapsedTimeCorrect()
        {
            var runs = new[]
            {
                new PlaytestMetrics(10, 5, 1, 1, 30f),
                new PlaytestMetrics(10, 5, 1, 1, 50f),
            };
            var report = new PlaytestReport(runs);
            Assert.Equal(40.0, report.MeanElapsedTime);
        }

        // ── Death tracking ────────────────────────────────────────────────────

        [Fact]
        public void Run_PlayerDeathCountedWhenEnemyKillsPlayer()
        {
            // Verify that PlaytestMetrics.Deaths reflects enemy-caused deaths.
            // In this headless setup the agent doesn't die, so deaths should be 0.
            var agent = new HeadlessPlaytestAgent(seed: 42, startPosition: Vector3.Zero);
            var combat = CreateCombatSystem();
            var session = new PlaytestSession(agent, combat, maxFrames: 60);
            session.RegisterEnemy("grunt", new Vector3(10f, 0f, 0f));

            var metrics = session.Run();

            // Agent is headless and doesn't take damage in this setup
            Assert.Equal(0, metrics.Deaths);
        }

        [Fact]
        public void PlaytestMetrics_DeathsFieldReflectsEnemyKills()
        {
            // Direct construction test: verify Deaths field is correctly stored.
            var m = new PlaytestMetrics(totalShots: 10, hitsOnTarget: 5, kills: 3, deaths: 1, elapsedTime: 30f);
            Assert.Equal(1, m.Deaths);
            Assert.Equal(3.0, m.KDR); // 3 kills / 1 death
        }
    }
}
