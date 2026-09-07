// EnemyWeaponSystem — 敌人武器系统（Core 纯逻辑层）。
// 职责：管理每个敌人的武器冷却/弹药，产出 EnemyFireRequest，供 CombatSystem 消费。
// 与 WeaponSystem 的区别：敌人使用无限弹药（经典魂斗罗设计），只需管理冷却。

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>敌人武器状态（可变）。</summary>
    public class EnemyWeaponState
    {
        public string EnemyId { get; }
        public string WeaponId { get; }
        public float CooldownTimer { get; set; }
        public float FireInterval { get; set; }

        public EnemyWeaponState(string enemyId, string weaponId, float fireInterval)
        {
            EnemyId = enemyId ?? throw new ArgumentException("EnemyId must not be null.");
            WeaponId = weaponId ?? throw new ArgumentException("WeaponId must not be null.");
            FireInterval = Math.Max(fireInterval, WeaponSystemConfig.MinFireIntervalS);
            CooldownTimer = 0f;
        }
    }

    /// <summary>
    /// 敌人武器系统 — 纯逻辑层，零 UnityEngine 依赖。
    /// 管理每个敌人的武器冷却计时器，并产出 EnemyFireRequest。
    /// 敌人使用无限弹药（经典魂斗罗设计），无需换弹。
    /// </summary>
    public class EnemyWeaponSystem
    {
        private readonly Dictionary<string, EnemyWeaponState> _states;
        private readonly Dictionary<string, WeaponDefinition> _weapons;
        private readonly IRandomProvider _random;

        /// <summary>当前待消费的射击请求列表（每帧由 ProcessFireRequests 清空）。</summary>
        public IReadOnlyList<EnemyFireRequest> PendingRequests => _pendingRequests;

        private readonly List<EnemyFireRequest> _pendingRequests = new();

        /// <summary>创建敌人武器系统。</summary>
        /// <param name="weapons">可用武器定义字典。</param>
        /// <param name="randomProvider">随机数提供者。</param>
        public EnemyWeaponSystem(Dictionary<string, WeaponDefinition> weapons, IRandomProvider randomProvider = null)
        {
            _weapons = weapons ?? throw new ArgumentException("Weapons dict must not be null.");
            _random = randomProvider ?? new DefaultRandomProvider();
            _states = new Dictionary<string, EnemyWeaponState>();
        }

        /// <summary>注册敌人武器（当敌人被生成时调用）。</summary>
        public void RegisterEnemy(string enemyId, string weaponId, float fireRate)
        {
            if (!_weapons.TryGetValue(weaponId, out var def))
                throw new ArgumentException($"Unknown weapon for enemy {enemyId}: {weaponId}");

            float fireInterval = Math.Max(1.0f / fireRate, def?.MinFireInterval ?? WeaponSystemConfig.MinFireIntervalS);
            _states[enemyId] = new EnemyWeaponState(enemyId, weaponId, fireInterval);
        }

        /// <summary>注销敌人武器（当敌人死亡时调用）。</summary>
        public void UnregisterEnemy(string enemyId)
        {
            _states.Remove(enemyId);
        }

        /// <summary>推进一帧计时器。dt 必须为正且有限。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            foreach (var kvp in _states)
            {
                if (kvp.Value.CooldownTimer > 0f)
                    kvp.Value.CooldownTimer = Math.Max(0f, kvp.Value.CooldownTimer - dt);
            }
        }

        /// <summary>
        /// 检查指定敌人是否可以射击。由 AiSystem.GetCommand() 或外部调用者使用。
        /// 返回 true 表示武器冷却完毕，可以射击。
        /// </summary>
        public bool IsWeaponReady(string enemyId)
        {
            return _states.TryGetValue(enemyId, out var ws) && ws.CooldownTimer <= 0f;
        }

        /// <summary>
        /// 生成射击请求（由 CombatSystem 在 AI 决定开火时调用）。
        /// 返回 true 表示请求已产出，false 表示武器不可用或敌人不存在。
        /// </summary>
        public bool TryFire(string enemyId, Vector3 origin, Vector3 direction)
        {
            if (!_states.TryGetValue(enemyId, out var ws))
                return false;

            if (ws.CooldownTimer > 0f)
                return false;

            // Apply spread to direction
            if (_weapons.TryGetValue(ws.WeaponId, out var def))
            {
                direction = ApplySpread(direction, def.Spread, _random);
            }

            // Set cooldown
            ws.CooldownTimer = ws.FireInterval;

            _pendingRequests.Add(new EnemyFireRequest(enemyId, ws.WeaponId, origin, direction));
            return true;
        }

        /// <summary>消费所有待处理的射击请求并清空队列。</summary>
        public List<EnemyFireRequest> ConsumeRequests()
        {
            var requests = new List<EnemyFireRequest>(_pendingRequests);
            _pendingRequests.Clear();
            return requests;
        }

        private static Vector3 ApplySpread(Vector3 direction, float spreadDeg, IRandomProvider random)
        {
            if (spreadDeg <= 0f) return direction;
            float spreadRad = spreadDeg * (float)Math.PI / 180f;
            float dx = (float)(random.NextDouble() * 2 - 1) * spreadRad;
            float dz = (float)(random.NextDouble() * 2 - 1) * spreadRad;
            return Vector3.Normalize(new Vector3(direction.X + dx, direction.Y, direction.Z + dz));
        }
    }
}
