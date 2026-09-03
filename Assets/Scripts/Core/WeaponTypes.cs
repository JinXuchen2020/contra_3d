using System;

namespace Contra3D.Core
{
    /// <summary>射击事件：由 WeaponSystem 产出，携带射击参数供 projectile/hitscan 系统消费。</summary>
    public readonly struct FireEvent
    {
        public string WeaponId { get; }
        public float Damage { get; }
        public float SpreadDeg { get; }
        public bool IsHitscan { get; }
        public float FireRate { get; }
        public float MagazineSize { get; }

        public FireEvent(string weaponId, float damage, float spreadDeg, bool isHitscan, float fireRate, float magazineSize)
        {
            WeaponId = weaponId ?? throw new ArgumentException("WeaponId must not be null.");
            if (damage < 0f) throw new ArgumentException($"Damage must be >= 0, got {damage}.", nameof(damage));
            if (spreadDeg < 0f) throw new ArgumentException($"Spread must be >= 0, got {spreadDeg}.", nameof(spreadDeg));
            WeaponId = weaponId;
            Damage = damage;
            SpreadDeg = spreadDeg;
            IsHitscan = isHitscan;
            FireRate = fireRate;
            MagazineSize = magazineSize;
        }
    }

    /// <summary>武器切换事件。</summary>
    public readonly struct SwitchEvent
    {
        public string FromWeaponId { get; }
        public string ToWeaponId { get; }

        public SwitchEvent(string fromWeaponId, string toWeaponId)
        {
            FromWeaponId = fromWeaponId ?? throw new ArgumentException("FromWeaponId must not be null.");
            ToWeaponId = toWeaponId ?? throw new ArgumentException("ToWeaponId must not be null.");
        }
    }

    /// <summary>武器系统操作结果。</summary>
    public enum WeaponActionResult
    {
        Success,
        OnCooldown,
        EmptyMagazine,
        Reloading,
        SwitchCooldown,
        UnknownWeapon,
    }

    /// <summary>武器系统配置常量。</summary>
    public static class WeaponSystemConfig
    {
        public const float MinFireIntervalS = 0.08f;
        public const float SwitchCooldownS = 0.5f;
        public const string DefaultWeaponId = "rifle_default";
    }
}
