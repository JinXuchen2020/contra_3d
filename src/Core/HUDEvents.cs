using System;

namespace Contra3D.Core
{
    /// <summary>得分增加事件。由武器击杀或任务完成触发。</summary>
    public readonly struct ScoreIncrementEvent
    {
        /// <summary>本次增加的分数。</summary>
        public int Amount { get; }

        /// <summary>累计后的新总分。</summary>
        public int NewScore { get; }

        public ScoreIncrementEvent(int amount, int newScore)
        {
            if (amount < 0) throw new ArgumentException("Amount must be >= 0.", nameof(amount));
            Amount = amount;
            NewScore = newScore;
        }
    }

    /// <summary>额外生命事件。当得分跨过 1UP 阈值（2000/5000/10000）时由 HUDUpdater 生成。</summary>
    public readonly struct ExtraLifeEvent
    {
        /// <summary>获得新生命后的总命数。</summary>
        public int NewLifeCount { get; }

        public ExtraLifeEvent(int newLifeCount)
        {
            if (newLifeCount < 0) throw new ArgumentException("NewLifeCount must be >= 0.", nameof(newLifeCount));
            NewLifeCount = newLifeCount;
        }
    }

    /// <summary>低血量事件。当健康比低于 0.25 时由 HUDUpdater 生成。</summary>
    public readonly struct LowHealthEvent
    {
        /// <summary>当前生命值与最大生命值的比值。</summary>
        public float HealthRatio { get; }

        public LowHealthEvent(float healthRatio)
        {
            if (healthRatio < 0f || healthRatio > 1f)
                throw new ArgumentException($"HealthRatio must be in [0,1], got {healthRatio}.", nameof(healthRatio));
            HealthRatio = healthRatio;
        }
    }
}
