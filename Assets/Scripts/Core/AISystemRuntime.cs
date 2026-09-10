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
    public sealed class AiRuntimeSystem
    {
        private readonly AiSystem _ai;

        /// <summary>当前活跃敌人数量。</summary>
        public int ActiveCount => _ai.ActiveCount;

        /// <summary>当前冲锋型敌人数量。</summary>
        public int RusherCount => _ai.RusherCount;

        /// <summary>当前使用的生成配置（只读）。</summary>
        public AISpawnConfig Config => _ai.Config;

        /// <summary>
        /// 创建 AI 系统实例（推荐用于测试，支持依赖注入）。
        /// </summary>
        /// <param name="definitions">敌人定义字典。</param>
        /// <param name="config">生成配置（可选，默认使用 <see cref="AISpawnConfig.Default"/>）。</param>
        /// <param name="randomProvider">随机数提供者（可选，默认使用 <see cref="DefaultRandomProvider"/>)。</param>
        public AiRuntimeSystem(Dictionary<string, EnemyDefinition> definitions, AISpawnConfig config = null, IRandomProvider randomProvider = null)
        {
            _ai = new AiSystem(definitions, randomProvider, config);
        }

        /// <summary>
        /// 创建空 AI 系统实例（支持延迟注册定义，用于测试）。
        /// </summary>
        public AiRuntimeSystem() : this(new Dictionary<string, EnemyDefinition>())
        {
        }

        /// <summary>
        /// 创建带配置的 AI 系统实例（用于隔离测试）。
        /// </summary>
        public AiRuntimeSystem(AISpawnConfig config) : this(new Dictionary<string, EnemyDefinition>(), config)
        {
        }

        /// <summary>
        /// 创建带随机数提供者的 AI 系统实例（用于隔离测试）。
        /// </summary>
        public AiRuntimeSystem(IRandomProvider randomProvider) : this(new Dictionary<string, EnemyDefinition>(), randomProvider: randomProvider)
        {
        }

        /// <summary>注册敌人定义。</summary>
        public void RegisterDefinition(EnemyDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _ai.Definitions[def.Id] = def;
        }

        /// <summary>设置玩家位置。</summary>
        public void SetPlayerPosition(Vector3 position) => _ai.SetPlayerPosition(position);

        /// <summary>刷兵请求（含排队机制与防门口霸营）。</summary>
        public bool TrySpawn(string enemyId, Vector3 position) => _ai.TrySpawn(enemyId, position);

        /// <summary>处理死亡事件并自动释放排队刷兵。</summary>
        public void OnEnemyDead(string enemyId) => _ai.OnEnemyDead(enemyId);

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
