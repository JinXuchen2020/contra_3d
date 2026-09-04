namespace Contra3D.Core.Playtest
{
    /// <summary>
    /// Immutable snapshot of a single headless playtest run.
    /// </summary>
    public readonly struct PlaytestMetrics
    {
        public int TotalShots { get; }
        public int HitsOnTarget { get; }
        public int Kills { get; }
        public int Deaths { get; }
        public float ElapsedTime { get; }

        public double Accuracy => TotalShots > 0 ? (double)HitsOnTarget / TotalShots : 0.0;
        public double KDR => Deaths > 0 ? (double)Kills / Deaths : Kills > 0 ? double.PositiveInfinity : 0.0;

        public PlaytestMetrics(int totalShots, int hitsOnTarget, int kills, int deaths, float elapsedTime)
        {
            TotalShots = totalShots;
            HitsOnTarget = hitsOnTarget;
            Kills = kills;
            Deaths = deaths;
            ElapsedTime = elapsedTime;
        }
    }

    /// <summary>
    /// Aggregate statistics across multiple runs.
    /// </summary>
    public readonly struct PlaytestReport
    {
        public PlaytestMetrics[] Runs { get; }
        public double MeanAccuracy { get; }
        public double MeanKillsPerRun { get; }
        public double MeanKDR { get; }
        public double MeanElapsedTime { get; }
        public int TotalRuns { get; }

        public PlaytestReport(PlaytestMetrics[] runs)
        {
            Runs = runs;
            TotalRuns = runs.Length;
            if (runs.Length == 0)
            {
                MeanAccuracy = 0;
                MeanKillsPerRun = 0;
                MeanKDR = 0;
                MeanElapsedTime = 0;
                return;
            }
            double accSum = 0, killsSum = 0, kdrSum = 0, timeSum = 0;
            foreach (var r in runs)
            {
                accSum += r.Accuracy;
                killsSum += r.Kills;
                kdrSum += r.KDR;
                timeSum += r.ElapsedTime;
            }
            int n = runs.Length;
            MeanAccuracy = accSum / n;
            MeanKillsPerRun = killsSum / n;
            MeanKDR = kdrSum / n;
            MeanElapsedTime = timeSum / n;
        }
    }
}
