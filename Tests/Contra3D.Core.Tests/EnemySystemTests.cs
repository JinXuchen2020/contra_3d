using System;
using System.Linq;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class EnemySystemTests
    {
        [Fact]
        public void BDD_rg_enemies_loaded()
        {
            // BDD: rg_enemies_loaded (T-QA-E2E-ENM)
            // The REAL DataLoader populated the enemy library from data/enemies/
            // (proves the production asset pipeline ran on the enemies path).
            // Verifies 5 enemy types loaded (grunt_soldier, charger_mutant,
            // turret_sniper, hound_runner, elite_gunner).
            // Expects enemy_library signal: enemy_count == 5.

            string testAssemblyDir = System.IO.Path.GetDirectoryName(typeof(EnemySystemTests).Assembly.Location);
            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
            string yamlPath = System.IO.Path.Combine(projectRoot, "data", "enemies", "enemies.yaml");

            var result = EnemyLoader.Load(yamlPath);
            var scoreTable = result.ScoreTable;

            // assert: enemy_count == 5 (all 5 enemy types loaded from data/enemies/)
            Assert.Equal(5, scoreTable.Count);

            // assert: all 5 expected enemy IDs are present
            Assert.Contains(scoreTable, kvp => kvp.Key == "grunt_soldier");
            Assert.Contains(scoreTable, kvp => kvp.Key == "charger_mutant");
            Assert.Contains(scoreTable, kvp => kvp.Key == "turret_sniper");
            Assert.Contains(scoreTable, kvp => kvp.Key == "hound_runner");
            Assert.Contains(scoreTable, kvp => kvp.Key == "elite_gunner");

            // assert: no extra or missing enemy IDs
            var loadedIds = scoreTable.Keys.OrderBy(k => k).ToArray();
            Assert.Equal(new[] { "charger_mutant", "elite_gunner", "grunt_soldier", "hound_runner", "turret_sniper" }, loadedIds);
        }
    }
}
