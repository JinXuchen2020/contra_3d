// T-SYS-001 (rendering/Core 镜像) — 武器数据模型。
// 设计来源: templates/system_design/rendering_system.md（Core 纯逻辑层，零 UnityEngine 依赖）。
// 镜像规则: 与 Assets/Scripts/Core/WeaponData.cs 内容一一对应；OS 工具扫描 src/，Unity 编译 Assets/。

using System;

namespace Contra3D.Core
{
    /// <summary>武器伤害类型（对齐 weapon 系统设计：hitscan 即时命中 / projectile 弹体 / melee 近战）。</summary>
    public enum WeaponType
    {
        /// <summary>即时射线命中（射线检测，无弹道飞行时间）。</summary>
        Hitscan,

        /// <summary>弹体（有飞行时间与速度，由弹道系统推进）。</summary>
        Projectile,

        /// <summary>近战（短距离范围判定）。</summary>
        Melee
    }

    /// <summary>
    /// 武器定义 — 纯 C# 数据模型（不可变）。构造时 strict fail-fast 校验，
    /// 非法值抛 <see cref="ArgumentException"/>。
    /// </summary>
    public sealed class WeaponDefinition
    {
        /// <summary>武器唯一标识（如 "rifle_m16"）。不可为空/空白。</summary>
        public string Id { get; }

        /// <summary>显示名。不可为空/空白。</summary>
        public string Name { get; }

        /// <summary>伤害类型。</summary>
        public WeaponType Type { get; }

        /// <summary>单发基础伤害。必须 &gt; 0。</summary>
        public float Damage { get; }

        /// <summary>射速（发/秒）。必须 &gt; 0。</summary>
        public float FireRate { get; }

        /// <summary>弹匣容量（发）。必须 &gt; 0。</summary>
        public int MagazineSize { get; }

        /// <summary>换弹时间（秒）。必须 &gt;= 0。</summary>
        public float ReloadTime { get; }

        /// <summary>散布角（度，半角）。必须 &gt;= 0。</summary>
        public float Spread { get; }

        /// <summary>
        /// 创建武器定义并做 fail-fast 参数校验。
        /// </summary>
        /// <exception cref="ArgumentException">任一参数非法（空串/非正值等）。</exception>
        public WeaponDefinition(
            string id,
            string name,
            WeaponType type,
            float damage,
            float fireRate,
            int magazineSize,
            float reloadTime,
            float spread)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Weapon Id must not be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Weapon Name must not be null or whitespace.", nameof(name));
            }

            if (damage <= 0f)
            {
                throw new ArgumentException($"Damage must be > 0, got {damage}.", nameof(damage));
            }

            if (fireRate <= 0f)
            {
                throw new ArgumentException($"FireRate must be > 0, got {fireRate}.", nameof(fireRate));
            }

            if (magazineSize <= 0)
            {
                throw new ArgumentException($"MagazineSize must be > 0, got {magazineSize}.", nameof(magazineSize));
            }

            if (reloadTime < 0f)
            {
                throw new ArgumentException($"ReloadTime must be >= 0, got {reloadTime}.", nameof(reloadTime));
            }

            if (spread < 0f)
            {
                throw new ArgumentException($"Spread must be >= 0, got {spread}.", nameof(spread));
            }

            Id = id;
            Name = name;
            Type = type;
            Damage = damage;
            FireRate = fireRate;
            MagazineSize = magazineSize;
            ReloadTime = reloadTime;
            Spread = spread;
        }
    }
}
