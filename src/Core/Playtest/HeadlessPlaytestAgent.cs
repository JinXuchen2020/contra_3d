using System;
using System.Collections.Generic;

namespace Contra3D.Core.Playtest
{
    /// <summary>
    /// Headless simulation agent that drives player input deterministically.
    /// Simulates run-and-gun movement + aimed fire against registered enemies.
    /// </summary>
    public class HeadlessPlaytestAgent
    {
        internal const float ForwardSpeed = 4.0f;
        private readonly Random _rng;
        private Vector3 _position;
        private float _moveTimer;
        private float _fireTimer;
        private Vector3 _currentMoveDir;
        internal const float FrameDt = 0.016f;
        private const float AimAccuracyDeg = 3.0f;

        public Vector3 Position => _position;

        public HeadlessPlaytestAgent(long seed, Vector3 startPosition)
        {
            _rng = new Random((int)seed);
            _position = startPosition;
            _moveTimer = 0f;
            _fireTimer = 0f;
            _currentMoveDir = Vector3.UnitZ;
        }

        /// <summary>
        /// Advance the agent one frame. Chooses a new move direction periodically
        /// and fires toward the nearest valid target if cooldown is ready.
        /// </summary>
        public (Vector3 moveDir, bool wantsFire, Vector3 aimDir) Update(List<(string Id, Vector3 Pos, float Radius)> aliveTargets)
        {
            _moveTimer += FrameDt;
            _fireTimer += FrameDt;

            // Change direction every 1–3 seconds
            if (_moveTimer >= 1.0f + (float)_rng.NextDouble() * 2.0f)
            {
                _moveTimer = 0f;
                _currentMoveDir = PickNewDirection();
            }

            // Fire if ready and targets exist (rifle_default fire rate = 7/s → interval ≈ 0.14s)
            bool wantsFire = _fireTimer >= 0.14f && aliveTargets.Count > 0;
            if (wantsFire) _fireTimer = 0f;

            Vector3 aimDir = Vector3.Zero;
            if (wantsFire)
            {
                var nearest = FindNearestTarget(aliveTargets);
                if (nearest.HasValue)
                    aimDir = AddAimError(Vector3.Normalize(nearest.Value.Item2 - _position), AimAccuracyDeg);
                else
                    aimDir = AddAimError(_currentMoveDir, AimAccuracyDeg);
            }

            return (_currentMoveDir, wantsFire, aimDir);
        }

        private (string, Vector3, float)? FindNearestTarget(List<(string Id, Vector3 Pos, float Radius)> targets)
        {
            (string, Vector3, float) closest = default;
            float closestDist = float.MaxValue;
            foreach (var t in targets)
            {
                float d = Vector3.Distance(t.Item2, _position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = t;
                }
            }
            return closestDist < 100f ? closest : null;
        }

        private Vector3 PickNewDirection()
        {
            float angle = (float)_rng.NextDouble() * 2.0f * (float)Math.PI;
            return new Vector3((float)Math.Cos(angle), 0, (float)Math.Sin(angle));
        }

        private Vector3 AddAimError(Vector3 dir, float spreadDeg)
        {
            float spreadRad = spreadDeg * ((float)Math.PI / 180f);
            float dx = (float)(_rng.NextDouble() * 2 - 1) * spreadRad;
            float dz = (float)(_rng.NextDouble() * 2 - 1) * spreadRad;
            return Vector3.Normalize(new Vector3(dir.X + dx, dir.Y, dir.Z + dz));
        }

        public void AdvancePosition(Vector3 direction, float speed, float dt)
        {
            _position += direction * speed * dt;
        }
    }
}
