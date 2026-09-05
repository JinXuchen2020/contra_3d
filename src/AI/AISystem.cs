using System;
using System.Collections.Generic;
using Contra3D.Core;

namespace Contra3D.AI
{
    /// <summary>
    /// 敌人 AI 系统 — Unity Runtime 层集成。
    /// 职责：桥接 Core.AiSystem 与 Unity GameObject，处理刷兵、感知、状态同步。
    /// 实例化设计（推荐用于测试），支持依赖注入。
    /// 通过 <see cref="Default"/> 提供共享实例以兼容现有静态调用风格。
    /// </summary>
    public sealed class AISystem
    {
        private readonly Dictionary<string, EnemyDefinition> _definitions = new();
        private readonly Dictionary<string, EnemyAIState> _states = new();
        private readonly Queue<string> _spawnQueue = new();
        private Vector3 _playerPosition;
        private int _activeCount;
        private int _rusherCount;
        private readonly AISpawnConfig _config;
        private readonly IRandomProvider _random;

        /// <summary>共享默认实例（向后兼容静态调用风格）。</summary>
        public static readonly AISystem Default = new AISystem();

        /// <summary>当前活跃敌人数量。</summary>
        public int ActiveCount => _activeCount;

        /// <summary>当前冲锋型敌人数量。</summary>
        public int RusherCount => _rusherCount;

        /// <summary>当前使用的生成配置（只读）。</summary>
        public AISpawnConfig Config => _config;

        /// <summary>
        /// 创建 AI 系统实例（推荐用于测试，支持依赖注入）。
        /// </summary>
        /// <param name="config">生成配置（可选，默认使用 <see cref="AISpawnConfig.Default"/>）。</param>
        /// <param name="randomProvider">随机数提供者（可选，默认使用 <see cref="DefaultRandomProvider"/>)。</param>
        public AISystem(AISpawnConfig config = null, IRandomProvider randomProvider = null)
        {
            _config = config ?? AISpawnConfig.Default;
            _random = randomProvider ?? new DefaultRandomProvider();
        }

        /// <summary>注册敌人定义。</summary>
        public void RegisterDefinition(EnemyDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _definitions[def.Id] = def;
        }

        /// <summary>设置玩家位置。</summary>
        public void SetPlayerPosition(Vector3 position)
        {
            _playerPosition = position;
        }

        /// <summary>刷兵请求（排队机制）。</summary>
        public bool TrySpawn(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                return false;

            // Anti-door camping: reject if too close to player
            float distToPlayer = Vector3.Distance(position, _playerPosition);
            if (distToPlayer < _config.AntiDoorCampingDistance)
                return false;

            // Check spawn caps
            int maxNormal = _config.MaxNormal;
            int maxRusher = _config.MaxRusher;

            if (def.AiType == AiType.Rusher && _rusherCount >= maxRusher)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            if (_activeCount >= maxNormal)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            // Spawn
            SpawnInternal(enemyId, position);
            return true;
        }

        private void SpawnInternal(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def)) return;

            var state = new EnemyAIState
            {
                EnemyId = enemyId,
                Position = position,
                AiType = def.AiType,
                State = AiState.Idle,
                Health = def.Health,
                MaxHealth = def.Health,
                Vigilance = 0f,
                PatrolTarget = position
            };

            _states[enemyId] = state;
            _activeCount++;
            if (def.AiType == AiType.Rusher) _rusherCount++;
        }

        /// <summary>处理死亡事件。</summary>
        public void OnEnemyDead(string enemyId)
        {
            if (_states.TryGetValue(enemyId, out var state))
            {
                _activeCount--;
                if (state.AiType == AiType.Rusher) _rusherCount--;
                _states.Remove(enemyId);

                // Release next queued spawn
                if (_spawnQueue.Count > 0 && _activeCount < _config.MaxNormal)
                {
                    string nextId = _spawnQueue.Dequeue();
                    SpawnInternal(nextId, state.Position);
                }
            }
        }

        /// <summary>推进一帧。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                return;

            var aliveStates = new List<EnemyAIState>(_states.Values);
            foreach (var state in aliveStates)
            {
                if (!state.IsAlive) continue;
                if (!_definitions.TryGetValue(state.EnemyId, out var def)) continue;

                UpdateState(state, def, dt);
            }
        }

        private void UpdateState(EnemyAIState state, EnemyDefinition def, float dt)
        {
            float distToPlayer = Vector3.Distance(state.Position, _playerPosition);
            bool playerInSight = distToPlayer <= def.VisionRange;

            // Update vigilance
            if (playerInSight)
                state.Vigilance = Math.Min(100f, state.Vigilance + def.VigilanceGainPerSecond * dt);
            else
                state.Vigilance = Math.Max(0f, state.Vigilance - def.VigilanceDecayPerSecond * dt);

            // Stagger recovery
            if (state.State == AiState.Staggered)
            {
                state.State = state.StaggerPrevState;
                state.StaggerPrevState = AiState.Idle;
            }

            // State transitions
            switch (def.AiType)
            {
                case AiType.Patrol: UpdatePatrol(state, def, dt, playerInSight, distToPlayer); break;
                case AiType.Chase: UpdateChase(state, def, dt, playerInSight, distToPlayer); break;
                case AiType.Sniper: UpdateSniper(state, def, dt, playerInSight, distToPlayer); break;
                case AiType.Rusher: UpdateRusher(state, def, dt, playerInSight, distToPlayer); break;
            }
        }

        private void UpdatePatrol(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
        {
            switch (state.State)
            {
                case AiState.Idle:
                    if (state.Vigilance >= def.AlertThreshold) state.State = AiState.Alert;
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
                    break;
                case AiState.Patrol:
                    if (Vector3.Distance(state.Position, state.PatrolTarget) < 1f)
                        state.PatrolTarget = state.Position + new Vector3(
                            _random.NextFloat(-10f, 10f), 0,
                            _random.NextFloat(-10f, 10f));
                    Vector3 dir = Vector3.Normalize(state.PatrolTarget - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (state.Vigilance >= def.AlertThreshold) state.State = AiState.Alert;
                    break;
            }
        }

        private void UpdateChase(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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
                        return;
                    }
                    Vector3 dir = Vector3.Normalize(_playerPosition - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (distToPlayer <= def.AttackRange) state.State = AiState.Combat;
                    break;
                case AiState.Combat:
                    if (!playerInSight || distToPlayer > def.AttackRange * 1.5f)
                        state.State = AiState.Chase;
                    break;
            }
        }

        private void UpdateSniper(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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
                        return;
                    }
                    if (distToPlayer < def.AttackRange * 0.5f)
                        state.State = AiState.Combat;
                    break;
                case AiState.Combat:
                    if (playerInSight && distToPlayer <= def.VisionRange)
                        state.State = AiState.Aim;
                    else
                        state.State = AiState.Idle;
                    break;
            }
        }

        private void UpdateRusher(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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
                        return;
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

        /// <summary>获取敌人指令。</summary>
        public AICommand GetCommand(string enemyId)
        {
            if (!_states.TryGetValue(enemyId, out var state) || !state.IsAlive)
                return AICommand.Idle;

            if (!_definitions.TryGetValue(enemyId, out var def))
                return AICommand.Idle;

            float distToPlayer = Vector3.Distance(state.Position, _playerPosition);

            switch (state.State)
            {
                case AiState.Combat:
                case AiState.Chase:
                case AiState.Rush:
                    if (distToPlayer <= def.AttackRange)
                        return AICommand.Attack(enemyId);
                    break;
            }
            return AICommand.Idle;
        }

        /// <summary>获取所有存活敌人状态。</summary>
        public IReadOnlyDictionary<string, EnemyAIState> GetStates() => _states;
    }
}