using System.Collections.Generic;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class WeaponSystemTests
    {
        private static Dictionary<string, WeaponDefinition> MakeWeapons()
        {
            var w = new Dictionary<string, WeaponDefinition>();
            w["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 30, 1.5f, 1.5f);
            w["shotgun"] = new WeaponDefinition("shotgun", "Shotgun", WeaponType.Projectile, 6f, 2f, 8, 2.2f, 12f);
            w["laser"] = new WeaponDefinition("laser", "Laser", WeaponType.Hitscan, 35f, 3f, 12, 2.5f, 0f);
            w["infinite"] = new WeaponDefinition("infinite", "Infinite", WeaponType.Hitscan, 5f, 10f, 9999, 0f, 0f);
            return w;
        }

        [Fact]
        public void InitialState_AmmoEqualsMagazine()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            Assert.Equal(30, ws.PrimaryAmmo);
            Assert.Null(ws.SecondaryId);
        }

        [Fact]
        public void Fire_ConsumesAmmo()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            for (int i = 0; i < 30; i++)
            {
                ws.Update(0.5f);
                var (result, _) = ws.ProcessFireRequest();
                Assert.Equal(WeaponActionResult.Success, result);
            }
            Assert.Equal(0, ws.PrimaryAmmo);
            ws.Update(0.5f);
            var (result2, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.EmptyMagazine, result2);
        }

        [Fact]
        public void Reload_FillsMagazine()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            for (int i = 0; i < 20; i++)
            {
                ws.Update(0.5f);
                ws.ProcessFireRequest();
            }
            Assert.Equal(10, ws.PrimaryAmmo);

            var (result, _) = ws.ProcessReloadRequest();
            Assert.Equal(WeaponActionResult.Success, result);
            Assert.True(ws.IsReloading);

            ws.Update(2.0f); // More than reload time (1.5s)
            Assert.False(ws.IsReloading);
            Assert.Equal(30, ws.PrimaryAmmo);
        }

        [Fact]
        public void FireDuringCooldown_Rejected()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["fast"] = new WeaponDefinition("fast", "Fast", WeaponType.Hitscan, 1f, 100f, 999, 0f, 0f);
            var ws = new WeaponSystem(weapons, "fast");
            ws.ProcessFireRequest();
            var (result, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.OnCooldown, result);
        }

        [Fact]
        public void HighFireRate_ClampedToMinInterval()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["superfast"] = new WeaponDefinition("superfast", "SuperFast", WeaponType.Hitscan, 1f, 200f, 999, 0f, 0f);
            var ws = new WeaponSystem(weapons, "superfast");
            ws.ProcessFireRequest();
            ws.Update(0.05f);
            var (result, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.OnCooldown, result);
            ws.Update(0.08f);
            (result, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.Success, result);
        }

        [Fact]
        public void Switch_Weapons()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            ws.ProcessSwitchRequest("shotgun");
            Assert.Equal("shotgun", ws.PrimaryId);
            Assert.Equal("rifle", ws.SecondaryId);
        }

        [Fact]
        public void SwitchCooldown_RejectsFire()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            ws.ProcessSwitchRequest("shotgun");
            ws.Update(0.1f);
            var (result, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.SwitchCooldown, result);
        }

        [Fact]
        public void FireDuringReload_Rejected()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle"] = new WeaponDefinition("rifle", "Rifle", WeaponType.Hitscan, 12f, 7f, 5, 2.0f, 1.5f);
            var ws = new WeaponSystem(weapons, "rifle");
            for (int i = 0; i < 5; i++)
            {
                ws.Update(0.5f);
                ws.ProcessFireRequest();
            }
            Assert.Equal(0, ws.PrimaryAmmo);
            ws.ProcessReloadRequest();
            ws.Update(0.5f);
            var (result, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.Reloading, result);
        }

        [Fact]
        public void LargeMagazine_DoesNotEmptyQuickly()
        {
            var ws = new WeaponSystem(MakeWeapons(), "infinite");
            for (int i = 0; i < 100; i++)
            {
                ws.Update(0.5f);
                var (result, _) = ws.ProcessFireRequest();
                Assert.Equal(WeaponActionResult.Success, result);
            }
            Assert.Equal(9899, ws.PrimaryAmmo);
        }

        [Fact]
        public void DeathReset_ResetsToDefault()
        {
            var ws = new WeaponSystem(MakeWeapons(), "rifle");
            ws.ProcessSwitchRequest("shotgun");
            ws.Update(10f);
            ws.OnDeathReset();
            Assert.Equal(WeaponSystemConfig.DefaultWeaponId, ws.PrimaryId);
            Assert.Null(ws.SecondaryId);
        }

        [Fact]
        public void RifleBaseline_StatsConformance()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition(
                "rifle_default",
                "默认突击步枪",
                WeaponType.Hitscan,
                12f,
                7f,
                30,
                1.5f,
                1.5f);

            var ws = new WeaponSystem(weapons, "rifle_default");

            Assert.Equal(WeaponType.Hitscan, weapons["rifle_default"].Type);
            Assert.Equal(12f, weapons["rifle_default"].Damage);
            Assert.Equal(7f, weapons["rifle_default"].FireRate);
            Assert.Equal(30, weapons["rifle_default"].MagazineSize);
            Assert.Equal(1.5f, weapons["rifle_default"].ReloadTime);
            Assert.Equal(1.5f, weapons["rifle_default"].Spread);

            ws.Update(0.5f);
            var (result, evt) = ws.ProcessFireRequest();

            Assert.Equal(WeaponActionResult.Success, result);
            Assert.Equal("rifle_default", evt.WeaponId);
        }

        [Fact]
        public void Magazine_EmptyMagazineRejectsFire()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition(
                "rifle_default",
                "默认突击步枪",
                WeaponType.Hitscan,
                12f,
                7f,
                30,
                1.5f,
                1.5f);

            var ws = new WeaponSystem(weapons, "rifle_default");

            for (int i = 0; i < 30; i++)
            {
                ws.Update(0.5f);
                var (result, _) = ws.ProcessFireRequest();
                Assert.Equal(WeaponActionResult.Success, result);
            }
            Assert.Equal(0, ws.PrimaryAmmo);

            ws.Update(0.5f);
            var (result31, _) = ws.ProcessFireRequest();
            Assert.Equal(WeaponActionResult.EmptyMagazine, result31);
        }

        [Fact]
        public void Magazine_ReloadRestoresAmmo()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["rifle_default"] = new WeaponDefinition(
                "rifle_default",
                "默认突击步枪",
                WeaponType.Hitscan,
                12f,
                7f,
                30,
                1.5f,
                1.5f);

            var ws = new WeaponSystem(weapons, "rifle_default");

            for (int i = 0; i < 30; i++)
            {
                ws.Update(0.5f);
                ws.ProcessFireRequest();
            }
            Assert.Equal(0, ws.PrimaryAmmo);

            var (reloadResult, _) = ws.ProcessReloadRequest();
            Assert.Equal(WeaponActionResult.Success, reloadResult);
            Assert.True(ws.IsReloading);

            ws.Update(1.5f);
            Assert.False(ws.IsReloading);
            Assert.Equal(30, ws.PrimaryAmmo);
        }

        [Fact]
        public void SpreadShot_FivePelletsOnFire()
        {
            var weapons = new Dictionary<string, WeaponDefinition>();
            weapons["spread_shot"] = new WeaponDefinition(
                "spread_shot",
                "Spread Shot",
                WeaponType.Projectile,
                6f,
                5f,
                8,
                2.0f,
                12f);

            var ws = new WeaponSystem(weapons, "spread_shot");
            Assert.Equal(8, ws.PrimaryAmmo);

            ws.Update(0.5f);
            var (result, evt) = ws.ProcessFireRequest();

            Assert.Equal(WeaponActionResult.Success, result);
            Assert.Equal("spread_shot", evt.WeaponId);
            Assert.Equal(WeaponType.Projectile, weapons["spread_shot"].Type);
            Assert.Equal(6f, evt.Damage);
            Assert.Equal(12f, evt.SpreadDeg);
            Assert.False(evt.IsHitscan);
            Assert.Equal(7, ws.PrimaryAmmo);
        }
    }
}
