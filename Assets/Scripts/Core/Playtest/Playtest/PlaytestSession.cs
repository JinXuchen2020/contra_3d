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
        private const float DefaultEnemyHealth = 100f;
        private readonly HeadlessPlaytestAgent _agent;
        private readonly CombatSystem _combat;
        private readonly HealthDamageSystem _healthDamage;
        private readonly int _maxFrames;

        private int _totalShots;
        private int _hitsOnTarget;
        private int _playerDeaths;
        private float _elapsed;

        public PlaytestSession(
            HeadlessPlaytestAgent agent,
            CombatSystem combat,
            int maxFrames = 9000)
            : this(agent, combat, null, maxFrames)
        {
        }

        public PlaytestSession(
            HeadlessPlaytestAgent agent,
            CombatSystem combat,
            HealthDamageSystem healthDamage,
            int maxFrames = 9000)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            // Use combat's internal HealthDamageSystem if no separate one provided
            // This ensures RegisterEnemy + ConsumeHit use the SAME entity registry
            _healthDamage = healthDamage ?? combat.GetHealthDamageSystem();
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
            _playerDeaths = 0;
            _elapsed = 0f;

            for (int frame = 0; frame < _maxFrames; frame++)
            {
                var aliveTargets = GetAliveTargets();
                var (moveDir, wantsFire, aimDir) = _agent.Update(aliveTargets);

                _agent.AdvancePosition(moveDir, HeadlessPlaytestAgent.ForwardSpeed, FrameDt);

                // Advance combat timers (weapon cooldowns, etc.)
                _combat.Update(FrameDt);

                // Advance projectiles and process collision hits
                _combat.UpdateProjectiles(FrameDt);

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

            // Count player deaths from health damage events where killer is not "player"
            foreach (var death in _healthDamage.Deaths)
            {
                if (death.KillerId != "player")
                    _playerDeaths++;
            }
            return new PlaytestMetrics(_totalShots, _hitsOnTarget, _combat.Kills, _playerDeaths, _elapsed);
        }

        /// <summary>
        /// Registers an enemy entity as a CombatSystem target.
        /// Call before Run().
        /// </summary>
        public string RegisterEnemy(string entityId, Vector3 position, float radius = 1.5f)
        {
            _combat.RegisterTarget(entityId, position, radius);
            _healthDamage.RegisterEntity(entityId, DefaultEnemyHealth);
            return entityId;
        }

        private IReadOnlyList<(string Id, Vector3 Pos, float Radius)> GetAliveTargets()
        {
            return _combat.GetTargets();
        }
    }
}
