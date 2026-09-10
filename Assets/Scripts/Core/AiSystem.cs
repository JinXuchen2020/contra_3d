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
        /// <summary>每敌人的当前武器 ID。</summary>
        private readonly Dictionary<string, string> _currentWeaponIds;
        private readonly Queue<string> _spawnQueue = new();
        private readonly AISpawnConfig _config;
        private int _activeCount;
        private int _rusherCount;

        public AiSystem(Dictionary<string, EnemyDefinition> definitions, IRandomProvider randomProvider = null, AISpawnConfig config = null)
        {
            _definitions = definitions ?? throw new ArgumentException("Definitions must not be null.");
            _states = new Dictionary<string, EnemyAIState>();
            _playerPosition = Vector3.Zero;
            _random = randomProvider ?? new DefaultRandomProvider();
            _currentWeaponIds = new Dictionary<string, string>();
            _config = config ?? AISpawnConfig.Default;
        }

        /// <summary>当前使用的生成配置（只读）。</summary>
        public AISpawnConfig Config => _config;

        /// <summary>敌人定义字典（内部访问，供 AiRuntimeSystem.RegisterDefinition 使用）。</summary>
        internal Dictionary<string, EnemyDefinition> Definitions => _definitions;

        /// <summary>当前活跃敌人数量。</summary>
        public int ActiveCount => _activeCount;

        /// <summary>当前冲锋型敌人数量。</summary>
        public int RusherCount => _rusherCount;

        /// <summary>
        /// 刷兵请求（含防门口霸营、上限检查、排队机制）。
        /// 返回 true 表示立即刷出，false 表示已入队或拒绝。
        /// </summary>
        public bool TrySpawn(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                return false;

            float distToPlayer = Vector3.Distance(position, _playerPosition);
            if (distToPlayer < _config.AntiDoorCampingDistance)
                return false;

            if (def.AiType == AiType.Rusher && RusherCount >= _config.MaxRusher)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            if (ActiveCount >= _config.MaxNormal)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            SpawnEnemy(enemyId, position);
            return true;
        }

        /// <summary>
        /// 处理敌人死亡，并从排队中释放下一个刷兵请求。
        /// </summary>
        public void OnEnemyDead(string enemyId)
        {
            var states = GetStates();
            if (!states.TryGetValue(enemyId, out var state)) return;

            _activeCount--;
            if (state.AiType == AiType.Rusher) _rusherCount--;
            var position = state.Position;
            RemoveEnemy(enemyId);

            if (_spawnQueue.Count > 0 && ActiveCount < _config.MaxNormal)
            {
                string nextId = _spawnQueue.Dequeue();
                if (_definitions.TryGetValue(nextId, out var nextDef))
                {
                    SpawnEnemy(nextId, position);
                }
            }
        }

        public void SetPlayerPosition(Vector3 position) => _playerPosition = position;

        public void SpawnEnemy(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                throw new ArgumentException($"Unknown enemy: {enemyId}");
            _states[enemyId] = new EnemyAIState();
            _states[enemyId].Reset(enemyId, def, position);
            _currentWeaponIds[enemyId] = def.Weapons.Count > 0 ? def.Weapons[0] : "";
            _activeCount++;
            if (def.AiType == AiType.Rusher) _rusherCount++;
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



        /// <summary>获取所有存活敌人状态。</summary>
        public IReadOnlyDictionary<string, EnemyAIState> GetStates() => _states;

        /// <summary>移除敌人（用于死亡事件处理）。</summary>
        public void RemoveEnemy(string enemyId) => _states.Remove(enemyId);

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