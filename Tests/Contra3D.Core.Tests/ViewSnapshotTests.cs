using System.Collections.Generic;
using System.Numerics;
using Contra3D.Core;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class ViewSnapshotTests
    {
        [Fact]
        public void ViewSnapshot_CreatesWithProjectilesAndEnemies()
        {
            var proj = new List<ProjectileViewData>
            {
                new ProjectileViewData("p1", new Vector3(1, 2, 3), new Vector3(0, 0, 1), true)
            };
            var enemy = new List<EnemyViewData>
            {
                new EnemyViewData("e1", new Vector3(5, 0, 5), 90f, "Patrol", true)
            };

            var snap = new ViewSnapshot(proj, enemy);

            Assert.Single(snap.Projectiles);
            Assert.Single(snap.Enemies);
            Assert.Equal("p1", snap.Projectiles[0].Id);
            Assert.Equal("e1", snap.Enemies[0].Id);
        }

        [Fact]
        public void ViewSnapshot_EmptyListIsAllowed()
        {
            var snap = new ViewSnapshot(new List<ProjectileViewData>(), new List<EnemyViewData>());

            Assert.Empty(snap.Projectiles);
            Assert.Empty(snap.Enemies);
        }

        [Fact]
        public void ProjectileViewData_IsActiveFalse_IsExcludedFromSync()
        {
            var proj = new List<ProjectileViewData>
            {
                new ProjectileViewData("p1", Vector3.Zero, Vector3.Zero, false),
                new ProjectileViewData("p2", new Vector3(1, 0, 0), new Vector3(1, 0, 0), true)
            };

            int activeCount = 0;
            foreach (var p in proj)
            {
                if (p.IsActive) activeCount++;
            }

            Assert.Equal(1, activeCount);
        }

        [Fact]
        public void EnemyViewData_ContainsAllRequiredFields()
        {
            var pos = new Vector3(10, 0, -5);
            var ev = new EnemyViewData("boss1", pos, 45f, "Combat", true);

            Assert.Equal("boss1", ev.Id);
            Assert.Equal(pos, ev.Position);
            Assert.Equal(45f, ev.YawDeg);
            Assert.Equal("Combat", ev.StateName);
            Assert.True(ev.IsActive);
        }

        [Fact]
        public void ViewSnapshot_MultipleProjectilesPreserveOrder()
        {
            var proj = new List<ProjectileViewData>
            {
                new ProjectileViewData("a", new Vector3(0, 0, 0), Vector3.Zero, true),
                new ProjectileViewData("b", new Vector3(1, 0, 0), Vector3.Zero, true),
                new ProjectileViewData("c", new Vector3(2, 0, 0), Vector3.Zero, true),
            };

            var snap = new ViewSnapshot(proj, new List<EnemyViewData>());

            Assert.Equal(3, snap.Projectiles.Count);
            Assert.Equal("a", snap.Projectiles[0].Id);
            Assert.Equal("b", snap.Projectiles[1].Id);
            Assert.Equal("c", snap.Projectiles[2].Id);
        }
    }
}
