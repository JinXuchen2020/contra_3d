using System;
using System.Collections.Generic;
using Contra3D.Core;

namespace Contra3D.Core
{
    /// <summary>
    /// 敌人 AI 系统 — Unity Runtime 层集成。
    /// 职责：桥接 Core.AiSystem 与 Unity GameObject，处理刷兵、感知、状态同步。
    /// 实例化设计（推荐用于测试），支持依赖注入。
    /// </summary>
    public sealed class AISystem
    {
        private readonly Dictionary<string, EnemyDefinition> _definitions = new();
        private readonly Queue<string> _spawnQueue = new();
        private Vector3 _playerPosition;
        private int _activeCount;
        private int _rusherCount;
        private readonly AISpawnConfig _config;
        private readonly IRandomProvider _random;
        private readonly AiSystem _ai;

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
            _ai = new AiSystem(_definitions, _random);
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
            _ai.SetPlayerPosition(position);
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
            if (def.AiType == AiType.Rusher && _rusherCount >= _config.MaxRusher)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            if (_activeCount >= _config.MaxNormal)
            {
                _spawnQueue.Enqueue(enemyId);
                return false;
            }

            // Spawn via core AiSystem
            _ai.SpawnEnemy(enemyId, position);
            _activeCount++;
            if (def.AiType == AiType.Rusher) _rusherCount++;
            return true;
        }

        /// <summary>处理死亡事件。</summary>
        public void OnEnemyDead(string enemyId)
        {
            var states = _ai.GetStates();
            if (!states.TryGetValue(enemyId, out var state)) return;

            _activeCount--;
            if (state.AiType == AiType.Rusher) _rusherCount--;
            var position = state.Position;
            _ai.RemoveEnemy(enemyId);

            // Release next queued spawn
            if (_spawnQueue.Count > 0 && _activeCount < _config.MaxNormal)
            {
                string nextId = _spawnQueue.Dequeue();
                if (_definitions.TryGetValue(nextId, out var nextDef))
                {
                    _ai.SpawnEnemy(nextId, position);
                    _activeCount++;
                    if (nextDef.AiType == AiType.Rusher) _rusherCount++;
                }
            }
        }

        /// <summary>推进一帧。</summary>
        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                return;

            _ai.Update(dt);
        }

        /// <summary>获取敌人指令。</summary>
        public AICommand GetCommand(string enemyId) => _ai.GetCommand(enemyId);

        /// <summary>获取所有存活敌人状态。</summary>
        public IReadOnlyDictionary<string, EnemyAIState> GetStates() => _ai.GetStates();
    }
}
