using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>命中事件：由 ProjectileSystem 产出，携带命中参数供 health_damage 消费。</summary>
    public readonly struct HitEvent
    {
        public int ProjectileId { get; }
        public string TargetId { get; }
        public float Damage { get; }
        public Vector3 HitPoint { get; }

        public HitEvent(int projectileId, string targetId, float damage, Vector3 hitPoint)
        {
            ProjectileId = projectileId;
            TargetId = targetId ?? throw new ArgumentException("TargetId must not be null.");
            if (damage < 0f) throw new ArgumentException($"Damage must be >= 0, got {damage}.", nameof(damage));
            ProjectileId = projectileId;
            TargetId = targetId;
            Damage = damage;
            HitPoint = hitPoint;
        }
    }

    /// <summary>弹体定义（不可变）。</summary>
    public sealed class ProjectileDefinition
    {
        public float Speed { get; }
        public float Radius { get; }
        public float Damage { get; }
        public float Lifetime { get; }
        public float MaxDistance { get; }
        public float HomingTurnRate { get; }
        public bool IsHitscan { get; }

        public ProjectileDefinition(float speed, float radius, float damage, float lifetime = 5f, float maxDistance = 500f, float homingTurnRate = 5f, bool isHitscan = false)
        {
            if (speed <= 0f) throw new ArgumentException($"Speed must be > 0, got {speed}.", nameof(speed));
            if (radius < 0f) throw new ArgumentException($"Radius must be >= 0, got {radius}.", nameof(radius));
            if (damage < 0f) throw new ArgumentException($"Damage must be >= 0, got {damage}.", nameof(damage));
            Speed = speed;
            Radius = radius;
            Damage = damage;
            Lifetime = lifetime;
            MaxDistance = maxDistance;
            HomingTurnRate = homingTurnRate;
            IsHitscan = isHitscan;
        }
    }

    /// <summary>弹体状态（可变，由 ProjectileSystem 管理）。</summary>
    public class ProjectileState
    {
        public int Id { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 TargetDirection { get; set; }
        public float BirthTime { get; set; }
        public float DistanceTraveled { get; set; }
        public bool IsHit { get; set; }
        public bool IsActive { get; set; }
        public string OwnerTag { get; set; }
        public ProjectileDefinition Def { get; set; }
        public HitEvent? HitResult { get; set; }

        public void Reset()
        {
            IsHit = false;
            IsActive = false;
            HitResult = null;
        }
    }

    /// <summary>弹体系统操作结果。</summary>
    public enum ProjectileActionResult
    {
        Success,
        PoolExhausted,
        InvalidDefinition,
    }

    /// <summary>弹体系统配置常量。</summary>
    public static class ProjectileSystemConfig
    {
        public const int MaxProjectiles = 200;
        public const float CollisionToleranceMultiplier = 1.5f;
        public const float OutOfBoundsDistance = 500f;
        public const float DefaultLifetime = 5f;
    }

    /// <summary>
    /// 弹体系统 — 对象池 + 弹道推进 + 碰撞检测。
    /// 纯逻辑层，零 UnityEngine 依赖。
    /// </summary>
    public class ProjectileSystem
    {
        private readonly ProjectileDefinition _defaultDef;
        private readonly ProjectileState[] _pool;
        private readonly Stack<int> _freeIndices;
        private readonly List<HitEvent> _hitEvents;
        private Vector3 _spawnOrigin;

        public int ActiveCount => _pool.Length - _freeIndices.Count;
        public IReadOnlyList<HitEvent> HitEvents => _hitEvents;

        public ProjectileSystem(ProjectileDefinition defaultDef)
        {
            _defaultDef = defaultDef ?? throw new ArgumentException("Default definition must not be null.");
            _pool = new ProjectileState[ProjectileSystemConfig.MaxProjectiles];
            _freeIndices = new Stack<int>(ProjectileSystemConfig.MaxProjectiles);
            _hitEvents = new List<HitEvent>();

            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = new ProjectileState { Id = i + 1, Def = defaultDef };
                _freeIndices.Push(i);
            }
        }

        /// <summary>推进一帧。dt 必须为正且有限。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            _hitEvents.Clear();

            foreach (var proj in _pool)
            {
                if (!proj.IsActive || proj.IsHit) continue;

                proj.BirthTime += dt;

                // Homing: rotate towards target direction
                if (proj.Def.HomingTurnRate > 0f && proj.TargetDirection != Vector3.Zero)
                {
                    proj.Direction = Vector3.Normalize(
                        Vector3.Lerp(proj.Direction, proj.TargetDirection, dt * proj.Def.HomingTurnRate));
                }

                // Move
                float moveDistance = proj.Def.Speed * dt;
                Vector3 oldPos = proj.Position;
                proj.Position += proj.Direction * moveDistance;
                proj.DistanceTraveled += moveDistance;

                // Lifetime check
                if (proj.BirthTime >= proj.Def.Lifetime)
                {
                    Recycle(proj);
                    continue;
                }

                // Out of bounds check
                if (Vector3.Distance(proj.Position, _spawnOrigin) > proj.Def.MaxDistance)
                {
                    Recycle(proj);
                    continue;
                }

                // Collision check (simplified: sphere vs point targets)
                CheckCollision(proj, oldPos, dt);
            }
        }

        /// <summary>生成弹体。返回 (result, projectileId)。</summary>
        public (ProjectileActionResult Result, int ProjectileId) SpawnProjectile(
            Vector3 origin, Vector3 direction, string ownerTag = "player", Vector3? targetPosition = null)
        {
            _spawnOrigin = origin;

            if (_freeIndices.Count == 0)
                return (ProjectileActionResult.PoolExhausted, -1);

            int idx = _freeIndices.Pop();
            var proj = _pool[idx];
            proj.Reset();
            proj.IsActive = true;
            proj.Position = origin;
            proj.Direction = Vector3.Normalize(direction);
            proj.TargetDirection = targetPosition.HasValue ? Vector3.Normalize(targetPosition.Value - origin) : Vector3.Zero;
            proj.BirthTime = 0f;
            proj.DistanceTraveled = 0f;
            proj.OwnerTag = ownerTag;
            proj.Def = _defaultDef;

            return (ProjectileActionResult.Success, proj.Id);
        }

        /// <summary>hitscan 瞬时命中检测。</summary>
        public (ProjectileActionResult Result, HitEvent? Hit) HitscanDetect(
            Vector3 origin, Vector3 direction, List<(string Id, Vector3 Position, float Radius)> targets,
            float maxDistance = 500f)
        {
            Vector3 dir = Vector3.Normalize(direction);
            float closestT = maxDistance;
            HitEvent? closestHit = null;

            foreach (var (id, pos, radius) in targets)
            {
                Vector3 toTarget = pos - origin;
                float proj = Vector3.Dot(toTarget, dir);
                if (proj <= 0f || proj > closestT) continue;

                Vector3 closestPoint = origin + dir * proj;
                float dist = Vector3.Distance(closestPoint, pos);

                if (dist <= radius)
                {
                    closestT = proj;
                    closestHit = new HitEvent(0, id, _defaultDef.Damage, closestPoint);
                }
            }

            if (closestHit.HasValue)
            {
                _hitEvents.Add(closestHit.Value);
                return (ProjectileActionResult.Success, closestHit);
            }

            return (ProjectileActionResult.Success, null);
        }

        private void CheckCollision(ProjectileState proj, Vector3 oldPos, float dt)
        {
            // Simplified: sphere sweep vs point targets (for now, no target list injected)
            // In runtime, this would be called by ProjectileManager with known enemy positions
        }

        private void Recycle(ProjectileState proj)
        {
            proj.IsHit = true;
            proj.IsActive = false;
            _freeIndices.Push(Array.IndexOf(_pool, proj));
        }
    }
}
