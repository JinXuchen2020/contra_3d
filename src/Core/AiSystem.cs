using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>敌人 AI 类型。</summary>
    public enum AiType
    {
        Patrol,
        Chase,
        Sniper,
        Rusher,
    }

    /// <summary>敌人 AI 状态。</summary>
    public enum AiState
    {
        Idle,
        Patrol,
        Alert,
        Combat,
        Chase,
        Aim,
        Rush,
        Staggered,
        Dead,
    }

    /// <summary>敌人定义（不可变）。</summary>
    public sealed class EnemyDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public float Health { get; }
        public float Speed { get; }
        public AiType AiType { get; }
        public float VisionRange { get; }
        public float VisionAngleDeg { get; }
        public float AttackRange { get; }
        public float AlertThreshold { get; }
        public float ComprehensionThreshold { get; }
        public float VigilanceGainPerSecond { get; }
        public float VigilanceDecayPerSecond { get; }
        public float SoundVigilanceGain { get; }
        public float HitVigilanceInstant { get; }

        public EnemyDefinition(
            string id, string name, float health, float speed, AiType aiType,
            float visionRange = 15f, float visionAngleDeg = 90f, float attackRange = 5f,
            float alertThreshold = 60f, float comprehensionThreshold = 100f,
            float vigilanceGainPerSecond = 20f, float vigilanceDecayPerSecond = 10f,
            float soundVigilanceGain = 35f, float hitVigilanceInstant = 100f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id must not be null or whitespace.", nameof(id));
            if (health <= 0f) throw new ArgumentException($"Health must be > 0, got {health}.", nameof(health));
            if (speed < 0f) throw new ArgumentException($"Speed must be >= 0, got {speed}.", nameof(speed));
            Id = id;
            Name = name;
            Health = health;
            Speed = speed;
            AiType = aiType;
            VisionRange = visionRange;
            VisionAngleDeg = visionAngleDeg;
            AttackRange = attackRange;
            AlertThreshold = alertThreshold;
            ComprehensionThreshold = comprehensionThreshold;
            VigilanceGainPerSecond = vigilanceGainPerSecond;
            VigilanceDecayPerSecond = vigilanceDecayPerSecond;
            SoundVigilanceGain = soundVigilanceGain;
            HitVigilanceInstant = hitVigilanceInstant;
        }
    }

    /// <summary>敌人 AI 状态（可变，由 AiSystem 管理）。</summary>
    public class EnemyAIState
    {
        public string EnemyId { get; set; }
        public Vector3 Position { get; set; }
        public AiType AiType { get; set; }
        public AiState State { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Vigilance { get; set; }
        public float TimeSinceLastStimulus { get; set; }
        public AiState StaggerPrevState { get; set; }
        public Vector3 PatrolTarget { get; set; }
        public bool IsAlive => State != AiState.Dead && Health > 0f;

        public void Reset(string enemyId, EnemyDefinition def, Vector3 startPosition)
        {
            EnemyId = enemyId;
            Position = startPosition;
            AiType = def.AiType;
            State = AiState.Idle;
            Health = def.Health;
            MaxHealth = def.Health;
            Vigilance = 0f;
            TimeSinceLastStimulus = 0f;
            PatrolTarget = startPosition;
        }
    }

    /// <summary>AI 输出指令（每帧由状态机产出）。</summary>
    public struct AICommand
    {
        public Vector3 MoveIntent;
        public bool FireRequest;
        public string TargetId;

        public static AICommand Idle => new AICommand { MoveIntent = Vector3.Zero, FireRequest = false };
        public static AICommand Move(Vector3 dir) => new AICommand { MoveIntent = dir, FireRequest = false };
        public static AICommand Attack(string targetId) => new AICommand { MoveIntent = Vector3.Zero, FireRequest = true, TargetId = targetId };
    }

    /// <summary>敌人 AI 系统 — 纯逻辑层，零 UnityEngine 依赖。</summary>
    public class AiSystem
    {
        private readonly Dictionary<string, EnemyDefinition> _definitions;
        private readonly Dictionary<string, EnemyAIState> _states;
        private Vector3 _playerPosition;

        public AiSystem(Dictionary<string, EnemyDefinition> definitions)
        {
            _definitions = definitions ?? throw new ArgumentException("Definitions must not be null.");
            _states = new Dictionary<string, EnemyAIState>();
            _playerPosition = Vector3.Zero;
        }

        public void SetPlayerPosition(Vector3 position) => _playerPosition = position;

        public void SpawnEnemy(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                throw new ArgumentException($"Unknown enemy: {enemyId}");
            _states[enemyId] = new EnemyAIState();
            _states[enemyId].Reset(enemyId, def, position);
        }

        public void TakeDamage(string enemyId, float damage)
        {
            if (!_states.TryGetValue(enemyId, out var state)) return;
            var def = GetDef(state);
            state.Health -= damage;
            state.Vigilance = def.HitVigilanceInstant;
            state.TimeSinceLastStimulus = 0f;
            if (state.Health <= 0f)
            {
                state.Health = 0f;
                state.State = AiState.Dead;
            }
            else
            {
                // Stagger briefly
                var prev = state.State;
                state.State = AiState.Staggered;
                state.StaggerPrevState = prev;
            }
        }

        /// <summary>EnemyDefinition accessor.</summary>
        private EnemyDefinition GetDef(EnemyAIState s) => _definitions[s.EnemyId];

        /// <summary>推进一帧。dt 必须为正且有限。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                throw new ArgumentOutOfRangeException(nameof(dt), "dt must be a positive finite number.");

            foreach (var state in _states.Values)
            {
                if (!state.IsAlive) continue;
                var def = GetDef(state);
                UpdateState(state, def, dt);
            }
        }

        private void UpdateState(EnemyAIState state, EnemyDefinition def, float dt)
        {
            float distToPlayer = Vector3.Distance(state.Position, _playerPosition);
            bool playerInSight = distToPlayer <= def.VisionRange;
            bool playerInAttackRange = distToPlayer <= def.AttackRange;

            // Update vigilance
            if (playerInSight)
                state.Vigilance = Math.Min(100f, state.Vigilance + def.VigilanceGainPerSecond * dt);
            else
                state.Vigilance = Math.Max(0f, state.Vigilance - def.VigilanceDecayPerSecond * dt);

            state.TimeSinceLastStimulus += dt;

            // Stagger recovery: return to previous state after one tick
            if (state.State == AiState.Staggered)
            {
                state.State = state.StaggerPrevState;
                state.StaggerPrevState = AiState.Idle;
            }

            // State transitions based on ai_type
            switch (def.AiType)
            {
                case AiType.Patrol:
                    UpdatePatrol(state, def, dt, playerInSight, distToPlayer);
                    break;
                case AiType.Chase:
                    UpdateChase(state, def, dt, playerInSight, distToPlayer, playerInAttackRange);
                    break;
                case AiType.Sniper:
                    UpdateSniper(state, def, dt, playerInSight, distToPlayer, playerInAttackRange);
                    break;
                case AiType.Rusher:
                    UpdateRusher(state, def, dt, playerInSight, distToPlayer, playerInAttackRange);
                    break;
            }
        }

        private void UpdatePatrol(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (state.Vigilance >= def.AlertThreshold)
                        state.State = AiState.Alert;
                    break;
                case AiState.Alert:
                    if (state.Vigilance >= def.ComprehensionThreshold)
                        state.State = AiState.Combat;
                    else if (state.Vigilance < def.AlertThreshold * 0.5f)
                        state.State = AiState.Idle;
                    break;
                case AiState.Combat:
                    if (!playerInSight || distToPlayer > def.VisionRange * 1.5f)
                    {
                        if (state.Vigilance <= 0f) state.State = AiState.Patrol;
                    }
                    else if (distToPlayer <= def.AttackRange)
                    {
                        // Attack
                    }
                    break;
                case AiState.Patrol:
                    // Move towards patrol target
                    if (Vector3.Distance(state.Position, state.PatrolTarget) < 1f)
                        state.PatrolTarget = state.Position + new Vector3((float)new Random().NextDouble() * 20f - 10f, 0, (float)new Random().NextDouble() * 20f - 10f);
                    Vector3 dir = Vector3.Normalize(state.PatrolTarget - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (state.Vigilance >= def.AlertThreshold)
                        state.State = AiState.Alert;
                    break;
            }
        }

        private void UpdateChase(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight) state.State = AiState.Chase;
                    break;
                case AiState.Chase:
                    if (!playerInSight)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    Vector3 dir = Vector3.Normalize(_playerPosition - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (playerInAttackRange) state.State = AiState.Combat;
                    break;
                case AiState.Combat:
                    if (!playerInSight || distToPlayer > def.AttackRange * 1.5f)
                        state.State = AiState.Chase;
                    // Attack logic handled by weapon_system
                    break;
            }
        }

        private void UpdateSniper(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight && distToPlayer <= def.VisionRange)
                        state.State = AiState.Aim;
                    break;
                case AiState.Aim:
                    if (!playerInSight || distToPlayer > def.VisionRange)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    if (distToPlayer < def.AttackRange * 0.5f)
                        state.State = AiState.Combat; // Reposition
                    // Aim logic: wait for clear shot
                    break;
                case AiState.Combat:
                    if (playerInSight && distToPlayer <= def.VisionRange)
                        state.State = AiState.Aim;
                    else
                        state.State = AiState.Idle;
                    break;
            }
        }

        private void UpdateRusher(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer, bool playerInAttackRange)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (playerInSight) state.State = AiState.Rush;
                    break;
                case AiState.Rush:
                    if (!playerInSight)
                    {
                        state.State = AiState.Idle;
                        break;
                    }
                    Vector3 rushDir = Vector3.Normalize(_playerPosition - state.Position);
                    state.Position += rushDir * def.Speed * dt;
                    if (distToPlayer <= def.AttackRange)
                    {
                        // Explode/Strike
                        state.State = AiState.Dead;
                    }
                    break;
            }
        }

    /// <summary>获取敌人的 AI 输出指令。</summary>
    public AICommand GetCommand(string enemyId)
        {
            if (!_states.TryGetValue(enemyId, out var state) || !state.IsAlive)
                return AICommand.Idle;
            var def = GetDef(state);
            float distToPlayer = Vector3.Distance(state.Position, _playerPosition);

            switch (state.State)
            {
                case AiState.Combat:
                case AiState.Chase:
                case AiState.Rush:
                    if (distToPlayer <= def.AttackRange)
                        return AICommand.Attack(enemyId);
                    break;
                case AiState.Patrol:
                case AiState.Alert:
                    if (distToPlayer <= def.AttackRange)
                        return AICommand.Attack(enemyId);
                    break;
            }
            return AICommand.Idle;
        }
    }

    /// <summary>随机数扩展方法。</summary>
    internal static class RandomExtensions
    {
        private static readonly Random Shared = new Random();
        public static float NextFloat(this Random _, float min, float max) => (float)Shared.NextDouble() * (max - min) + min;
    }
}
