using System;
using System.Collections.Generic;
using Contra3D.Combat;

namespace Contra3D.Core.Playtest
{
    /// <summary>
    /// Headless playtest session — wires CombatSystem + HeadlessAgent into a deterministic simulation.
    /// Enemies are registered as CombatSystem targets; the agent fires hitscan rounds each frame.
    /// Metrics track accuracy, kill count, and elapsed time.
    /// </summary>
    public class PlaytestSession
    {
        private const float FrameDt = 0.016f;
        private readonly HeadlessPlaytestAgent _agent;
        private readonly CombatSystem _combat;
        private readonly int _maxFrames;

        private int _totalShots;
        private int _hitsOnTarget;
        private int _kills;
        private float _elapsed;

        public PlaytestSession(
            HeadlessPlaytestAgent agent,
            CombatSystem combat,
            int maxFrames = 9000)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _maxFrames = maxFrames;
        }

        /// <summary>
        /// Runs one full simulation and returns metrics.
        /// Enemies must be pre-registered via RegisterEnemy().
        /// </summary>
        public PlaytestMetrics Run()
        {
            _totalShots = 0;
            _hitsOnTarget = 0;
            _kills = 0;
            _elapsed = 0f;

            for (int frame = 0; frame < _maxFrames; frame++)
            {
                var aliveTargets = GetAliveTargets();
                var (moveDir, wantsFire, aimDir) = _agent.Update(aliveTargets);

                _agent.AdvancePosition(moveDir, HeadlessPlaytestAgent.ForwardSpeed, FrameDt);

                // Fire at configured rate
                if (wantsFire && aliveTargets.Count > 0)
                {
                    _totalShots++;
                    var (_, _, hit) = _combat.ProcessFireRequest(_agent.Position, aimDir);
                    if (hit.HasValue)
                    {
                        _hitsOnTarget++;
                    }
                }

                _elapsed += FrameDt;
                if (_elapsed >= 120f) break; // cap at 2 minutes
            }

            return new PlaytestMetrics(_totalShots, _hitsOnTarget, _kills, 0, _elapsed);
        }

        /// <summary>
        /// Registers an enemy entity as a CombatSystem target.
        /// Call before Run().
        /// </summary>
        public string RegisterEnemy(string entityId, Vector3 position, float radius = 1.5f)
        {
            _combat.RegisterTarget(entityId, position, radius);
            return entityId;
        }

        private List<(string Id, Vector3 Pos, float Radius)> GetAliveTargets()
        {
            // CombatSystem exposes no public target list API;
            // we iterate via the internal target registry using the field name.
            // TargetEntry is a private struct in CombatSystem, so we read raw entries.
            var field = typeof(CombatSystem).GetField("_targetRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return new List<(string, Vector3, float)>();

            var registry = (System.Collections.Generic.Dictionary<string, object>)field.GetValue(_combat);
            if (registry == null) return new List<(string, Vector3, float)>();

            var result = new List<(string, Vector3, float)>();
            foreach (var kvp in registry)
            {
                var entry = kvp.Value;
                var posProp = entry.GetType().GetProperty("Position");
                var radiusProp = entry.GetType().GetProperty("Radius");
                if (posProp == null || radiusProp == null) continue;
                var pos = (Vector3)posProp.GetValue(entry);
                var radius = (float)radiusProp.GetValue(entry);
                result.Add((kvp.Key, pos, radius));
            }
            return result;
        }
    }
}
