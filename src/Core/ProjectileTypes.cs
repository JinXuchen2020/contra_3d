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

    /// <summary>弹体系统配置 — 支持从 YAML 加载，默认值与常量保持一致。</summary>
    public sealed class ProjectileSystemConfig
    {
        public int MaxProjectiles { get; set; } = 200;
        public float CollisionToleranceMultiplier { get; set; } = 1.5f;
        public float OutOfBoundsDistance { get; set; } = 500f;
        public float DefaultLifetime { get; set; } = 5f;

        /// <summary>内置常量回退值（向后兼容）。</summary>
        public const int MaxProjectilesConst = 200;
        public const float CollisionToleranceMultiplierConst = 1.5f;
        public const float OutOfBoundsDistanceConst = 500f;
        public const float DefaultLifetimeConst = 5f;

        /// <summary>默认实例（等价于内置常量）。</summary>
        public static readonly ProjectileSystemConfig Default = new ProjectileSystemConfig();

        /// <summary>从 YAML 字符串加载配置。null/空时返回默认实例。</summary>
        public static ProjectileSystemConfig LoadFromYaml(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent)) return Default;
            var cfg = new ProjectileSystemConfig();
            foreach (var line in yamlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                var parts = trimmed.Split(':', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var val = parts[1].Trim();
                if (float.TryParse(val, out float fv))
                {
                    if (key == "max_projectiles") cfg.MaxProjectiles = (int)fv;
                    else if (key == "collision_tolerance_multiplier") cfg.CollisionToleranceMultiplier = fv;
                    else if (key == "out_of_bounds_distance") cfg.OutOfBoundsDistance = fv;
                    else if (key == "default_lifetime") cfg.DefaultLifetime = fv;
                }
            }
            return cfg;
        }
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
            _pool = new ProjectileState[ProjectileSystemConfig.Default.MaxProjectiles];
            _freeIndices = new Stack<int>(ProjectileSystemConfig.Default.MaxProjectiles);
            _hitEvents = new List<HitEvent>();

            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = new ProjectileState { Id = i + 1, Def = defaultDef };
                _freeIndices.Push(i);
            }
        }

        /// <summary>推进一帧。dt 必须为正且有限。</summary>
        public void Update(float dt, IReadOnlyList<(string Id, Vector3 Position, float Radius)> targets = null)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            _hitEvents.Clear();

            foreach (var proj in _pool)
            {
                if (!proj.IsActive || proj.IsHit) continue;

                proj.BirthTime += dt;

                // Homing: rotate towards target direction (manual lerp to avoid allocations)
                if (proj.Def.HomingTurnRate > 0f && proj.TargetDirection != Vector3.Zero)
                {
                    Vector3 dir = proj.Direction;
                    Vector3 targetDir = proj.TargetDirection;
                    float t = dt * proj.Def.HomingTurnRate;
                    dir.X += (targetDir.X - dir.X) * t;
                    dir.Y += (targetDir.Y - dir.Y) * t;
                    dir.Z += (targetDir.Z - dir.Z) * t;
                    float len = (float)System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
                    if (len > 0.0001f)
                    {
                        dir.X /= len;
                        dir.Y /= len;
                        dir.Z /= len;
                    }
                    proj.Direction = dir;
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

                // Collision check (sphere sweep vs point targets)
                CheckCollision(proj, oldPos, dt, targets);
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

        private void CheckCollision(ProjectileState proj, Vector3 oldPos, float dt, IReadOnlyList<(string Id, Vector3 Position, float Radius)> targets)
        {
            if (targets == null || targets.Count == 0) return;

            // Sphere sweep: expand collision radius by max(1.0× frame displacement, 0.1m) to prevent tunneling
            float frameDisplacement = proj.Def.Speed * dt;
            float sweepRadius = proj.Def.Radius + Math.Max(1.0f * frameDisplacement, 0.1f);

            foreach (var (id, pos, enemyRadius) in targets)
            {
                // Skip if already hit this projectile
                if (proj.IsHit) return;

                Vector3 toCenter = pos - oldPos;
                Vector3 delta = proj.Position - oldPos; // total displacement this frame

                // Project enemy center onto displacement ray
                float dirLen = (float)System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
                if (dirLen < 0.0001f) continue; // no movement this frame

                Vector3 dir = new Vector3(delta.X / dirLen, delta.Y / dirLen, delta.Z / dirLen);
                float t = Vector3.Dot(toCenter, dir);

                if (t < 0f || t > dirLen) continue; // closest point not on segment

                // Closest point on segment to enemy center
                Vector3 closest = oldPos + dir * t;
                float dist = (float)System.Math.Sqrt(
                    (closest.X - pos.X) * (closest.X - pos.X) +
                    (closest.Y - pos.Y) * (closest.Y - pos.Y) +
                    (closest.Z - pos.Z) * (closest.Z - pos.Z));

                if (dist <= sweepRadius + enemyRadius)
                {
                    // Register hit at intersection point
                    float hitT = Math.Max(0f, Math.Min(t, dirLen));
                    Vector3 hitPoint = oldPos + dir * hitT;
                    var hit = new HitEvent(proj.Id, id, proj.Def.Damage, hitPoint);
                    _hitEvents.Add(hit);
                    proj.IsHit = true;
                    proj.HitResult = hit;
                    return;
                }
            }
        }

        private void Recycle(ProjectileState proj)
        {
            proj.IsHit = true;
            proj.IsActive = false;
            _freeIndices.Push(Array.IndexOf(_pool, proj));
        }
    }
}
