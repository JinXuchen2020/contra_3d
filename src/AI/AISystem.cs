using System;
using System.Collections.Generic;
using Contra3D.Core;

namespace Contra3D.AI
{
    /// <summary>
    /// 敌人 AI 系统 — Unity Runtime 层集成。
    /// 职责：桥接 Core.AiSystem 与 Unity GameObject，处理刷兵、感知、状态同步。
    /// </summary>
    public static class AISystem
    {
        private static readonly Dictionary<string, EnemyDefinition> _definitions = new();
        private static readonly Dictionary<string, EnemyAIState> _states = new();
        private static Vector3 _playerPosition;
        private static readonly Queue<string> _spawnQueue = new();
        private static int _activeCount;
        private static int _rusherCount;

        public static int ActiveCount => _activeCount;
        public static int RusherCount => _rusherCount;

        /// <summary>注册敌人定义。</summary>
        public static void RegisterDefinition(EnemyDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _definitions[def.Id] = def;
        }

        /// <summary>设置玩家位置。</summary>
        public static void SetPlayerPosition(Vector3 position)
        {
            _playerPosition = position;
        }

        /// <summary>刷兵请求（排队机制）。</summary>
        public static bool TrySpawn(string enemyId, Vector3 position)
        {
            if (!_definitions.TryGetValue(enemyId, out var def))
                return false;

            // Anti-door camping: reject if too close to player
            float distToPlayer = Vector3.Distance(position, _playerPosition);
            if (distToPlayer < 5f)
                return false;

            // Check spawn caps
            int maxNormal = 12;
            int maxRusher = 4;

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

        private static void SpawnInternal(string enemyId, Vector3 position)
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
        public static void OnEnemyDead(string enemyId)
        {
            if (_states.TryGetValue(enemyId, out var state))
            {
                _activeCount--;
                if (state.AiType == AiType.Rusher) _rusherCount--;
                _states.Remove(enemyId);

                // Release next queued spawn
                if (_spawnQueue.Count > 0 && _activeCount < 12)
                {
                    string nextId = _spawnQueue.Dequeue();
                    SpawnInternal(nextId, state.Position);
                }
            }
        }

        /// <summary>推进一帧。</summary>
        public static void Update(float dt)
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

        private static void UpdateState(EnemyAIState state, EnemyDefinition def, float dt)
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

        private static void UpdatePatrol(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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
                            (float)(new Random().NextDouble() * 20 - 10), 0,
                            (float)(new Random().NextDouble() * 20 - 10));
                    Vector3 dir = Vector3.Normalize(state.PatrolTarget - state.Position);
                    state.Position += dir * def.Speed * dt;
                    if (state.Vigilance >= def.AlertThreshold) state.State = AiState.Alert;
                    break;
            }
        }

        private static void UpdateChase(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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

        private static void UpdateSniper(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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

        private static void UpdateRusher(EnemyAIState state, EnemyDefinition def, float dt, bool playerInSight, float distToPlayer)
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
        public static AICommand GetCommand(string enemyId)
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
        public static IReadOnlyDictionary<string, EnemyAIState> GetStates() => _states;
    }
}
