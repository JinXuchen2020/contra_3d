using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    /// <summary>
    /// 武器系统状态机 — 纯逻辑层，零 UnityEngine 依赖。
    /// 职责：射击冷却/弹匣/换弹/切换状态管理，产出 FireEvent 或 SwitchEvent。
    /// </summary>
    public class WeaponSystem
    {
        private readonly Dictionary<string, WeaponDefinition> _weapons;
        private string _primaryId;
        private string _secondaryId;
        private readonly Dictionary<string, int> _ammo;
        private readonly Dictionary<string, float> _cooldownTimer;
        private readonly Dictionary<string, float> _reloadTimer;
        private bool _isReloading;
        private string _reloadingSlot;
        private float _switchCooldownTimer;

        public string PrimaryId => _primaryId;
        public string SecondaryId => _secondaryId;
        public bool IsReloading => _isReloading;
        public int PrimaryAmmo => _ammo.TryGetValue(_primaryId, out var a) ? a : 0;
        public int SecondaryAmmo => _secondaryId != null && _ammo.TryGetValue(_secondaryId, out var sa) ? sa : 0;

        public WeaponSystem(Dictionary<string, WeaponDefinition> weapons, string primaryId = null)
        {
            _weapons = weapons ?? throw new ArgumentException("Weapons dict must not be null.");
            _primaryId = primaryId ?? WeaponSystemConfig.DefaultWeaponId;
            if (!_weapons.TryGetValue(_primaryId, out var primaryDef))
                throw new ArgumentException($"Unknown primary weapon: {_primaryId}");

            _secondaryId = null;
            _ammo = new Dictionary<string, int>();
            _cooldownTimer = new Dictionary<string, float>();
            _reloadTimer = new Dictionary<string, float>();

            // Initialize primary
            _ammo[_primaryId] = primaryDef.MagazineSize <= 0 ? -1 : primaryDef.MagazineSize;
            _cooldownTimer[_primaryId] = 0f;
            _reloadTimer[_primaryId] = 0f;
            _isReloading = false;
            _reloadingSlot = null;
            _switchCooldownTimer = 0f;
        }

        /// <summary>推进一帧计时器。dt 必须为正且有限。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            // Decrease cooldown timers
            foreach (var kvp in _cooldownTimer)
            {
                if (kvp.Value > 0f)
                    _cooldownTimer[kvp.Key] = Math.Max(0f, kvp.Value - dt);
            }

            // Decrease switch cooldown
            if (_switchCooldownTimer > 0f)
                _switchCooldownTimer = Math.Max(0f, _switchCooldownTimer - dt);

            // Handle reloading
            if (_isReloading && _reloadingSlot != null)
            {
                _reloadTimer[_reloadingSlot] -= dt;
                if (_reloadTimer[_reloadingSlot] <= 0f)
                {
                    _reloadTimer[_reloadingSlot] = 0f;
                    _isReloading = false;
                    string reloadingId = _reloadingSlot;
                    _reloadingSlot = null;
                    // Fill magazine
                    if (_weapons.TryGetValue(reloadingId, out var def) && def.MagazineSize > 0)
                        _ammo[reloadingId] = def.MagazineSize;
                }
            }
        }

        /// <summary>处理射击请求。返回 (result, event)。</summary>
        public (WeaponActionResult Result, FireEvent @Event) ProcessFireRequest(string slot = null)
        {
            string targetId = slot ?? _primaryId;
            if (!_weapons.TryGetValue(targetId, out var def))
                return (WeaponActionResult.UnknownWeapon, default);

            // Check switching cooldown blocks all fire
            if (_switchCooldownTimer > 0f)
                return (WeaponActionResult.SwitchCooldown, default);

            // Check reloading
            if (_isReloading)
                return (WeaponActionResult.Reloading, default);

            // Check cooldown
            if (_cooldownTimer.TryGetValue(targetId, out var cd) && cd > 0f)
                return (WeaponActionResult.OnCooldown, default);

            // Check ammo (magazine_size <= 0 means infinite)
            int ammo = _ammo.TryGetValue(targetId, out var a) ? a : 0;
            if (def.MagazineSize > 0 && ammo <= 0)
                return (WeaponActionResult.EmptyMagazine, default);

            // Consume ammo
            if (def.MagazineSize > 0)
                _ammo[targetId] = ammo - 1;

            // Set cooldown (max of 1/fire_rate and min interval)
            float fireInterval = Math.Max(1.0f / def.FireRate, WeaponSystemConfig.MinFireIntervalS);
            _cooldownTimer[targetId] = fireInterval;

            var evt = new FireEvent(targetId, def.Damage, def.Spread, def.Type == WeaponType.Hitscan, def.FireRate, def.MagazineSize);
            return (WeaponActionResult.Success, evt);
        }

        /// <summary>请求切换到指定武器。返回 (result, event)。</summary>
        public (WeaponActionResult Result, SwitchEvent @Event) ProcessSwitchRequest(string targetId)
        {
            if (!_weapons.TryGetValue(targetId, out var _))
                return (WeaponActionResult.UnknownWeapon, default);

            if (_switchCooldownTimer > 0f)
                return (WeaponActionResult.SwitchCooldown, default);

            if (targetId == _primaryId)
                return (WeaponActionResult.Success, new SwitchEvent(_primaryId, _primaryId));

            // Swap primary and secondary
            string prevPrimary = _primaryId;
            _primaryId = targetId;
            _secondaryId = prevPrimary;

            // Ensure ammo entry exists
            if (!_ammo.ContainsKey(_primaryId))
                _ammo[_primaryId] = _weapons[_primaryId].MagazineSize > 0 ? _weapons[_primaryId].MagazineSize : -1;
            if (!_cooldownTimer.ContainsKey(_primaryId))
                _cooldownTimer[_primaryId] = 0f;

            _switchCooldownTimer = WeaponSystemConfig.SwitchCooldownS;
            return (WeaponActionResult.Success, new SwitchEvent(prevPrimary, targetId));
        }

        /// <summary>请求换弹。</summary>
        public (WeaponActionResult Result, bool Reloaded) ProcessReloadRequest(string slot = null)
        {
            string targetId = slot ?? _primaryId;
            if (!_weapons.TryGetValue(targetId, out var def))
                return (WeaponActionResult.UnknownWeapon, false);

            if (def.MagazineSize <= 0)
                return (WeaponActionResult.Success, true); // Infinite ammo, nothing to reload

            int currentAmmo = _ammo.TryGetValue(targetId, out var a) ? a : 0;
            if (currentAmmo >= def.MagazineSize)
                return (WeaponActionResult.Success, true); // Already full

            if (_isReloading)
                return (WeaponActionResult.Reloading, false);

            _isReloading = true;
            _reloadingSlot = targetId;
            _reloadTimer[targetId] = def.ReloadTime;
            return (WeaponActionResult.Success, false);
        }

        /// <summary>死亡重置：恢复到默认武器，清空特殊武器槽。</summary>
        public void OnDeathReset()
        {
            _secondaryId = null;
            _primaryId = WeaponSystemConfig.DefaultWeaponId;
            _ammo.Clear();
            _cooldownTimer.Clear();
            _reloadTimer.Clear();
            _isReloading = false;
            _reloadingSlot = null;
            _switchCooldownTimer = 0f;

            if (_weapons.TryGetValue(_primaryId, out var def))
            {
                _ammo[_primaryId] = def.MagazineSize > 0 ? def.MagazineSize : -1;
                _cooldownTimer[_primaryId] = 0f;
                _reloadTimer[_primaryId] = 0f;
            }
        }
    }
}
