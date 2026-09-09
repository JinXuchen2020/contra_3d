using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{

    /// <summary>敌人 AI 系统 — 纯逻辑层，零 UnityEngine 依赖。</summary>
    public partial class AiSystem
    {
        private readonly Dictionary<string, EnemyDefinition> _definitions;
        private readonly Dictionary<string, EnemyAIState> _states;
        private Vector3 _playerPosition;
        private readonly IRandomProvider _random;
        /// <summary>每敌人类的当前武器冷却计时器（秒）。</summary>
        private readonly Dictionary<string, float> _fireCooldownTimers;
        /// <summary>每敌人的当前武器 ID。</summary>
        private readonly Dictionary<string, string> _currentWeaponIds;

        public AiSystem(Dictionary<string, EnemyDefinition> definitions, IRandomProvider randomProvider = null)
        {
            _definitions = definitions ?? throw new ArgumentException("Definitions must not be null.");
            _states = new Dictionary<string, EnemyAIState>();
            _playerPosition = Vector3.Zero;
            _random = randomProvider ?? new DefaultRandomProvider();
            _fireCooldownTimers = new Dictionary<string, float>();
            _currentWeaponIds = new Dictionary<string, string>();
        }

        public void SetPlayerPosition(Vector3 position) => _playerPosition = position;

        public void SpawnEnemy(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                throw new ArgumentException($"Unknown enemy: {enemyId}");
            _states[enemyId] = new EnemyAIState();
            _states[enemyId].Reset(enemyId, def, position);
            // Initialize fire cooldown and weapon
            _fireCooldownTimers[enemyId] = 0f;
            _currentWeaponIds[enemyId] = def.Weapons.Count > 0 ? def.Weapons[0] : "";
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
                // Stagger briefly — remember current state to recover after
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

        /// <summary>减少指定敌人的武器冷却计时器（供 EnemyWeaponSystem 每帧调用）。</summary>
        public void DecrementFireCooldown(string enemyId, float dt)
        {
            if (_fireCooldownTimers.TryGetValue(enemyId, out var cd) && cd > 0f)
                _fireCooldownTimers[enemyId] = Math.Max(0f, cd - dt);
        }

        /// <summary>设置指定敌人的武器冷却计时器（供 EnemyWeaponSystem 射击后调用）。</summary>
        public void SetFireCooldown(string enemyId, float seconds)
        {
            _fireCooldownTimers[enemyId] = seconds;
        }

        /// <summary>获取指定敌人的武器冷却计时器（供 EnemyWeaponSystem 查询是否可射击）。</summary>
        public float GetFireCooldown(string enemyId)
        {
            return _fireCooldownTimers.TryGetValue(enemyId, out var cd) ? cd : 0f;
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
                // Recover to the state that was active before being staggered
                state.State = state.StaggerPrevState != AiState.Idle ? state.StaggerPrevState : AiState.Combat;
                state.StaggerPrevState = AiState.Idle;
            }

            // State transitions based on ai_type
            switch (def.AiType)
            {
                case AiType.Patrol:
                    UpdatePatrol(state, def, dt, playerInSight, distToPlayer, playerInAttackRange);
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
                    {
                        string weaponId = def.Weapons.Count > 0 ? def.Weapons[0] : "";
                        return AICommand.Attack(enemyId, weaponId);
                    }
                    break;
                case AiState.Patrol:
                case AiState.Alert:
                    if (distToPlayer <= def.AttackRange)
                    {
                        string weaponId = def.Weapons.Count > 0 ? def.Weapons[0] : "";
                        return AICommand.Attack(enemyId, weaponId);
                    }
                    break;
                case AiState.Aim:
                    if (def.Weapons.Count > 0)
                    {
                        string weaponId = def.Weapons[0];
                        return AICommand.Attack(enemyId, weaponId);
                    }
                    break;
            }
            return AICommand.Idle;
        }
    }
}