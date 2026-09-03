using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    /// <summary>生命值变化事件。</summary>
    public readonly struct HealthChangeEvent
    {
        public string EntityId { get; }
        public float DamageDealt { get; }
        public float NewHealth { get; }
        public bool IsDead { get; }

        public HealthChangeEvent(string entityId, float damageDealt, float newHealth, bool isDead)
        {
            EntityId = entityId ?? throw new ArgumentException("EntityId must not be null.");
            DamageDealt = damageDealt;
            NewHealth = newHealth;
            IsDead = isDead;
        }
    }

    /// <summary>死亡事件。</summary>
    public readonly struct DeathEvent
    {
        public string EntityId { get; }
        public string KillerId { get; }
        public string DropTableId { get; }

        public DeathEvent(string entityId, string killerId, string dropTableId)
        {
            EntityId = entityId ?? throw new ArgumentException("EntityId must not be null.");
            KillerId = killerId;
            DropTableId = dropTableId;
        }
    }

    /// <summary>生命值组件。</summary>
    public class HealthComponent
    {
        public string Id { get; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public float Armor { get; set; }
        public float PartMultiplier { get; set; } // 默认 1.0（躯干）
        public float InvulnTimer { get; set; }
        public bool IsDead { get; private set; }

        public HealthComponent(string id, float maxHealth, float armor = 0f, float partMultiplier = 1.0f)
        {
            if (maxHealth <= 0f) throw new ArgumentException($"MaxHealth must be > 0, got {maxHealth}.", nameof(maxHealth));
            Id = id ?? throw new ArgumentException("Id must not be null.");
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            Armor = armor;
            PartMultiplier = partMultiplier;
            InvulnTimer = 0f;
            IsDead = false;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            if (InvulnTimer > 0f) return;
            float finalDamage = DamageCalculator.Calculate(damage, PartMultiplier, Armor);
            CurrentHealth = Math.Max(0f, CurrentHealth - finalDamage);
            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                CurrentHealth = 0f;
            }
        }

        public void Update(float dt)
        {
            if (InvulnTimer > 0f)
                InvulnTimer = Math.Max(0f, InvulnTimer - dt);
        }

        public void OnDead() => IsDead = true;
    }

    /// <summary>
    /// 生命伤害系统 — 唯一有权修改生命值的模块。
    /// 纯逻辑层，零 UnityEngine 依赖。
    /// </summary>
    public class HealthDamageSystem
    {
        private readonly Dictionary<string, HealthComponent> _entities;
        private readonly List<HealthChangeEvent> _healthChanges;
        private readonly List<DeathEvent> _deaths;

        public IReadOnlyList<HealthChangeEvent> HealthChanges => _healthChanges;
        public IReadOnlyList<DeathEvent> Deaths => _deaths;

        public HealthDamageSystem()
        {
            _entities = new Dictionary<string, HealthComponent>();
            _healthChanges = new List<HealthChangeEvent>();
            _deaths = new List<DeathEvent>();
        }

        public void RegisterEntity(string entityId, float maxHealth, float armor = 0f, float partMultiplier = 1.0f)
        {
            if (_entities.ContainsKey(entityId))
                throw new ArgumentException($"Entity already registered: {entityId}");
            _entities[entityId] = new HealthComponent(entityId, maxHealth, armor, partMultiplier);
        }

        /// <summary>处理命中事件。返回 (healthChange, deathEvent)。</summary>
        public (HealthChangeEvent Change, DeathEvent? Death) ProcessHit(string entityId, float damage, string killerId = null, string dropTableId = null)
        {
            if (!_entities.TryGetValue(entityId, out var component))
                return (default, null);

            component.TakeDamage(damage);

            var change = new HealthChangeEvent(entityId, damage, component.CurrentHealth, component.IsDead);
            _healthChanges.Add(change);

            DeathEvent? death = null;
            if (component.IsDead)
            {
                death = new DeathEvent(entityId, killerId ?? "unknown", dropTableId ?? "none");
                _deaths.Add(death.Value);
            }

            return (change, death);
        }

        /// <summary>推进一帧。dt 必须为正且有限。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            foreach (var comp in _entities.Values)
                comp.Update(dt);

            _healthChanges.Clear();
            _deaths.Clear();
        }

        public bool IsDead(string entityId) => _entities.TryGetValue(entityId, out var c) && c.IsDead;
    }
}
