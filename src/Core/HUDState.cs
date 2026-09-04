using System;

namespace Contra3D.Core
{
    /// <summary>
    /// HUD 不可变快照 — 由 HealthChangeEvent / DeathEvent / ScoreIncrementEvent 驱动更新。
    /// 每次变更返回新实例，旧实例保持不变。
    /// </summary>
    public readonly struct HUDState
    {
        /// <summary>当前生命值。</summary>
        public float Health { get; }

        /// <summary>最大生命值（常量，初始化后不变）。</summary>
        public float MaxHealth { get; }

        /// <summary>剩余命数。</summary>
        public int Lives { get; }

        /// <summary>累计得分。</summary>
        public int Score { get; }

        /// <summary>当前武器 ID。</summary>
        public string CurrentWeaponId { get; }

        /// <summary>是否处于低血量状态（Health / MaxHealth &lt; 0.25）。</summary>
        public bool LowHealth => MaxHealth > 0f && (Health / MaxHealth) < 0.25f;

        /// <summary>当前准星扩散角度（度），默认 4.0f。</summary>
        public float CrosshairSpread { get; }

        public HUDState(float health, float maxHealth, int lives, int score, string currentWeaponId, float crosshairSpread = 4.0f)
        {
            if (maxHealth <= 0f) throw new ArgumentException($"MaxHealth must be > 0, got {maxHealth}.", nameof(maxHealth));
            if (health < 0f || health > maxHealth)
                throw new ArgumentException($"Health must be in [0, MaxHealth], got {health}.", nameof(health));
            if (lives < 0) throw new ArgumentException("Lives must be >= 0.", nameof(lives));
            if (score < 0) throw new ArgumentException("Score must be >= 0.", nameof(score));
            if (crosshairSpread < 0f) throw new ArgumentException("CrosshairSpread must be >= 0.", nameof(crosshairSpread));

            Health = health;
            MaxHealth = maxHealth;
            Lives = lives;
            Score = score;
            CurrentWeaponId = currentWeaponId;
            CrosshairSpread = crosshairSpread;
        }

        /// <summary>
        /// 从初始参数创建 HUDState。
        /// </summary>
        public static HUDState FromInitialState(float health, int lives, int score, string weaponId = null)
        {
            return new HUDState(health, health, lives, score, weaponId);
        }

        /// <summary>
        /// 返回健康值更新后的新 HUDState（不可变）。
        /// </summary>
        public HUDState WithHealth(float newHealth)
        {
            if (newHealth < 0f) newHealth = 0f;
            if (newHealth > MaxHealth) newHealth = MaxHealth;
            return new HUDState(newHealth, MaxHealth, Lives, Score, CurrentWeaponId);
        }

        /// <summary>
        /// 返回命数更新后的新 HUDState（不可变）。
        /// </summary>
        public HUDState WithLives(int newLives)
        {
            return new HUDState(Health, MaxHealth, newLives, Score, CurrentWeaponId);
        }

        /// <summary>
        /// 返回得分更新后的新 HUDState（不可变）。
        /// </summary>
        public HUDState WithScore(int newScore)
        {
            return new HUDState(Health, MaxHealth, Lives, newScore, CurrentWeaponId);
        }

        /// <summary>
        /// 返回准星扩散角度更新后的新 HUDState（不可变）。
        /// </summary>
        public HUDState WithCrosshairSpread(float newSpread)
        {
            if (newSpread < 0f) newSpread = 0f;
            return new HUDState(Health, MaxHealth, Lives, Score, CurrentWeaponId, newSpread);
        }

        /// <summary>
        /// 返回武器 ID 更新后的新 HUDState（不可变）。
        /// </summary>
        public HUDState WithWeapon(string newWeaponId)
        {
            return new HUDState(Health, MaxHealth, Lives, Score, newWeaponId);
        }

        public override string ToString() =>
            $"HUDState(health={Health}, maxHealth={MaxHealth}, lives={Lives}, score={Score}, weapon={CurrentWeaponId}, lowHealth={LowHealth}, crosshairSpread={CrosshairSpread})";
    }
}
