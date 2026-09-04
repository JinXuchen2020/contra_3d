// T-RUNTIME-001 — CombatSystem 战斗系统（Core 纯逻辑层）。
// 设计来源: templates/system_design/combat_system.md
// 职责: 协调 weapon_system / projectile_system / health_damage_system，不直接扣血，不直产生弹体。
// 依赖: WeaponSystem, ProjectileSystem, HealthDamageSystem
// 验收: 见 combat_system.md 验收清单；单元测试覆盖每个分支。

using System;
using System.Collections.Generic;
using Contra3D.Core;

namespace Contra3D.Combat
{
    /// <summary>
    /// 战斗系统 — 纯逻辑协调层，零 UnityEngine 依赖。
    /// 接收玩家射击请求，路由到 WeaponSystem → ProjectileSystem/HitDetection → HealthDamageSystem。
    /// </summary>
    public class CombatSystem
    {
        private readonly WeaponSystem _weaponSystem;
        private readonly ProjectileSystem _projectileSystem;
        private readonly HealthDamageSystem _healthDamageSystem;
        private readonly Dictionary<string, TargetEntry> _targetRegistry;
        private readonly List<(string TargetId, Vector3 Position, float Radius)> _targetListCache;

        // 计分与掉落
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public IReadOnlyList<DeathEvent> RecentDeaths => _recentDeaths;

        private readonly List<DeathEvent> _recentDeaths = new();
        private const int MaxRecentDeaths = 64;

        private struct TargetEntry
        {
            public Vector3 Position;
            public float Radius;
        }

        /// <summary>创建战斗系统。</summary>
        public CombatSystem(
            WeaponSystem weaponSystem,
            ProjectileSystem projectileSystem,
            HealthDamageSystem healthDamageSystem)
        {
            _weaponSystem = weaponSystem ?? throw new ArgumentNullException(nameof(weaponSystem));
            _projectileSystem = projectileSystem ?? throw new ArgumentNullException(nameof(projectileSystem));
            _healthDamageSystem = healthDamageSystem ?? throw new ArgumentNullException(nameof(healthDamageSystem));
            _targetRegistry = new Dictionary<string, TargetEntry>();
            _targetListCache = new List<(string, Vector3, float)>();
        }

        // ──────────────────────────────────────────────────────────────────────
        // 目标注册 / 注销
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>注册可被命中的实体。</summary>
        public void RegisterTarget(string entityId, Vector3 position, float radius = 1f)
        {
            if (string.IsNullOrWhiteSpace(entityId))
                throw new ArgumentException("EntityId must not be null or whitespace.", nameof(entityId));
            _targetRegistry[entityId] = new TargetEntry { Position = position, Radius = Math.Max(radius, 0.1f) };
        }

        /// <summary>注销实体（死亡/销毁时调用）。</summary>
        public void UnregisterTarget(string entityId)
        {
            _targetRegistry.Remove(entityId);
        }

        /// <summary>更新实体位置（每帧调用）。</summary>
        public void UpdateTargetPosition(string entityId, Vector3 position)
        {
            if (_targetRegistry.TryGetValue(entityId, out var entry))
                _targetRegistry[entityId] = new TargetEntry { Position = position, Radius = entry.Radius };
        }

        // ──────────────────────────────────────────────────────────────────────
        // 射击入口
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>处理玩家射击请求（主武器槽）。</summary>
        public (WeaponActionResult Result, FireEvent @Event, HitEvent? Hit) ProcessFireRequest(Vector3 origin, Vector3 direction)
        {
            var (result, fireEvent) = _weaponSystem.ProcessFireRequest(slot: null);
            if (result != WeaponActionResult.Success)
                return (result, default, null);

            direction = ApplySpread(direction, fireEvent.SpreadDeg);

            if (fireEvent.IsHitscan)
            {
                var hit = HitscanShoot(origin, direction, fireEvent.Damage);
                return (result, fireEvent, hit);
            }
            else
            {
                var (projResult, projId) = _projectileSystem.SpawnProjectile(origin, direction, "player");
                if (projResult != ProjectileActionResult.Success)
                    return (result, fireEvent, null);
                return (result, fireEvent, null);
            }
        }

        /// <summary>处理副武器射击请求。</summary>
        public (WeaponActionResult Result, FireEvent? Event, HitEvent? Hit) ProcessSecondaryFireRequest(Vector3 origin, Vector3 direction)
        {
            if (_weaponSystem.SecondaryId == null)
                return (WeaponActionResult.UnknownWeapon, null, null);

            var (result, fireEvent) = _weaponSystem.ProcessFireRequest(slot: _weaponSystem.SecondaryId);
            if (result != WeaponActionResult.Success)
                return (result, null, null);

            direction = ApplySpread(direction, fireEvent.SpreadDeg);

            if (fireEvent.IsHitscan)
            {
                var hit = HitscanShoot(origin, direction, fireEvent.Damage);
                return (result, fireEvent, hit);
            }
            else
            {
                var (projResult, projId) = _projectileSystem.SpawnProjectile(origin, direction, "player");
                if (projResult != ProjectileActionResult.Success)
                    return (result, fireEvent, null);
                return (result, fireEvent, null);
            }
        }

        /// <summary>切换武器。</summary>
        public (WeaponActionResult Result, SwitchEvent Event) ProcessSwitchRequest(string weaponId)
        {
            return _weaponSystem.ProcessSwitchRequest(weaponId);
        }

        /// <summary>请求换弹。</summary>
        public (WeaponActionResult Result, bool Reloaded) ProcessReloadRequest(string slot = null)
        {
            return _weaponSystem.ProcessReloadRequest(slot);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 命中消费
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 消费 ProjectileSystem 产出的命中事件，转发到 HealthDamageSystem。
        /// 调用时机：每次 ProjectileSystem.Update() 之后。
        /// </summary>
        public void ProcessHitEvents(string dropTableId = "default")
        {
            var hitEvents = _projectileSystem.HitEvents;
            foreach (var hit in hitEvents)
            {
                ConsumeHit(hit, dropTableId);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // 推进
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>推进一帧（仅更新武器冷却与弹体，CombatSystem 本身无状态计时器）。</summary>
        public void Update(float dt)
        {
            _weaponSystem.Update(dt);
        }

        /// <summary>推进弹体（由 GameLoop 每帧调用）。</summary>
        public void UpdateProjectiles(float dt)
        {
            _projectileSystem.Update(dt);
            ProcessHitEvents();
        }

        // ──────────────────────────────────────────────────────────────────────
        // 重置
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>死亡重置：恢复默认武器，清除目标注册。</summary>
        public void OnPlayerDeathReset()
        {
            _weaponSystem.OnDeathReset();
            _targetRegistry.Clear();
            _recentDeaths.Clear();
            Score = 0;
            Kills = 0;
        }

        // ──────────────────────────────────────────────────────────────────────
        // 内部实现
        // ──────────────────────────────────────────────────────────────────────

        private HitEvent? HitscanShoot(Vector3 origin, Vector3 direction, float damage)
        {
            BuildTargetList();
            var (result, hit) = _projectileSystem.HitscanDetect(origin, direction, _targetListCache, maxDistance: 200f);
            if (!hit.HasValue) return null;
            ConsumeHit(hit.Value, "default");
            return hit;
        }

        private void ConsumeHit(HitEvent hit, string dropTableId)
        {
            var (change, death) = _healthDamageSystem.ProcessHit(hit.TargetId, hit.Damage, killerId: "player", dropTableId: dropTableId);
            if (death.HasValue)
            {
                var d = death.Value;
                _recentDeaths.Add(d);
                if (_recentDeaths.Count > MaxRecentDeaths)
                    _recentDeaths.RemoveAt(0);
                Kills++;
                Score += ComputeScore(d.EntityId);
                UnregisterTarget(d.EntityId);
            }
        }

        private int ComputeScore(string enemyId)
        {
            return enemyId switch
            {
                "grunt_soldier" => 100,
                "charger_mutant" => 200,
                "turret_sniper" => 150,
                "hound_runner" => 120,
                "elite_gunner" => 500,
                _ => 50
            };
        }

        private void BuildTargetList()
        {
            _targetListCache.Clear();
            foreach (var kvp in _targetRegistry)
            {
                _targetListCache.Add((kvp.Key, kvp.Value.Position, kvp.Value.Radius));
            }
        }

        private static Vector3 ApplySpread(Vector3 direction, float spreadDeg)
        {
            if (spreadDeg <= 0f) return direction;
            float spreadRad = spreadDeg * (float)Math.PI / 180f;
            float dx = (float)(new Random().NextDouble() * 2 - 1) * spreadRad;
            float dz = (float)(new Random().NextDouble() * 2 - 1) * spreadRad;
            return Vector3.Normalize(new Vector3(direction.X + dx, direction.Y, direction.Z + dz));
        }
    }
}
