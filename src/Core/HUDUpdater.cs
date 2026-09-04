using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    /// <summary>
    /// HUD 事件驱动状态机 — 纯逻辑层，零 UnityEngine 依赖。
    /// 响应 HealthChangeEvent / DeathEvent / ScoreIncrementEvent 并更新 HUDState。
    /// </summary>
    public class HUDUpdater
    {
        /// <summary>1UP 阈值序列（2000 / 5000 / 10000）。</summary>
        private static readonly int[] ExtraLifeThresholds = { 2000, 5000, 10000 };

        private HUDState _state;
        private int _nextThresholdIndex;
        private bool _lowHealthFired;

        public HUDState State => _state;

        /// <summary>订阅到的 ExtraLifeEvent 列表（Process 产生，供外部消费）。</summary>
        public List<ExtraLifeEvent> GeneratedExtraLifeEvents { get; }

        /// <summary>订阅到的 LowHealthEvent 列表（Process 产生，供外部消费）。</summary>
        public List<LowHealthEvent> GeneratedLowHealthEvents { get; }

        public HUDUpdater(HUDState initialState)
        {
            if (initialState.Lives < 0)
                throw new ArgumentException("Initial lives must be >= 0.", nameof(initialState));
            _state = initialState;
            _nextThresholdIndex = 0;
            _lowHealthFired = false;
            GeneratedExtraLifeEvents = new List<ExtraLifeEvent>();
            GeneratedLowHealthEvents = new List<LowHealthEvent>();
        }

        /// <summary>处理生命值变化事件。</summary>
        public void Process(HealthChangeEvent @event)
        {
            var newState = _state.WithHealth(@event.NewHealth);
            _state = newState;

            // 低血量标志：只在刚进入低血量区间时触发一次
            if (newState.LowHealth && !_lowHealthFired)
            {
                _lowHealthFired = true;
                GeneratedLowHealthEvents.Add(new LowHealthEvent(newState.Health / newState.MaxHealth));
            }
            else if (!newState.LowHealth)
            {
                _lowHealthFired = false;
            }
        }

        /// <summary>处理死亡事件。命数减一；归零时重置为初始状态（Respawn）。</summary>
        public void Process(DeathEvent @event)
        {
            int newLives = _state.Lives - 1;
            if (newLives < 0)
            {
                // Game over — 重置为初始状态（从 FromInitialState 创建的生命值）
                _state = HUDState.FromInitialState(_state.MaxHealth, 0, _state.Score, _state.CurrentWeaponId);
            }
            else
            {
                _state = _state.WithLives(newLives);
            }

            // 死亡后重置低血量标志，避免残留
            _lowHealthFired = false;
        }

        /// <summary>
        /// 处理得分增加事件。若跨越 1UP 阈值则追加 ExtraLifeEvent 并增加命数。
        /// </summary>
        public void Process(ScoreIncrementEvent @event)
        {
            int newScore = @event.NewScore;
            _state = _state.WithScore(newScore);

            // 检查 1UP 阈值
            while (_nextThresholdIndex < ExtraLifeThresholds.Length && newScore >= ExtraLifeThresholds[_nextThresholdIndex])
            {
                _nextThresholdIndex++;
                int bonusLife = _state.Lives + 1;
                _state = _state.WithLives(bonusLife);
                GeneratedExtraLifeEvents.Add(new ExtraLifeEvent(bonusLife));
            }

            // 得分变化不影响低血量判断
        }

        /// <summary>
        /// 手动切换武器，返回新的 HUDState。
        /// </summary>
        public void SetWeapon(string weaponId)
        {
            _state = _state.WithWeapon(weaponId);
        }

        /// <summary>重置到指定初始状态。</summary>
        public void Reset(HUDState initialState)
        {
            if (initialState.Lives < 0)
                throw new ArgumentException("Initial lives must be >= 0.", nameof(initialState));
            _state = initialState;
            _nextThresholdIndex = 0;
            _lowHealthFired = false;
            GeneratedExtraLifeEvents.Clear();
            GeneratedLowHealthEvents.Clear();
        }
    }
}
