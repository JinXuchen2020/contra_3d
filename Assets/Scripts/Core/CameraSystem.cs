using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    public enum CameraMode { ThirdPersonFollow, Fixed, Splitscreen, Minimap }

    public struct CameraConstraint
    {
        public float PitchMinDeg; public float PitchMaxDeg;
        public float YawMinDeg; public float YawMaxDeg; public bool YawUnlimited;
        public static CameraConstraint Default() => new CameraConstraint { PitchMinDeg = -60f, PitchMaxDeg = 80f, YawMinDeg = float.NegativeInfinity, YawMaxDeg = float.PositiveInfinity, YawUnlimited = true };
    }

    public class CameraRig
    {
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 TargetPosition { get; internal set; }
        public float FOV { get; set; }
        public float Damping { get; internal set; } = 5.0f;
        public float OffsetsRight { get; internal set; } = 0.6f;
        public float OffsetsBack { get; internal set; } = 2.5f;
        public float OffsetsUp { get; internal set; } = 1.7f;
        public CameraMode Mode { get; set; } = CameraMode.ThirdPersonFollow;
        private Vector3 _modeStartPos, _modeEndPos, _modeStartRot, _modeEndRot;
        private float _modeTransitionElapsed, _modeTransitionDuration;
        private bool _modeTransitionActive;
        private float _trauma;
        public float Trauma => _trauma;
        public float TraumaDecayRate { get; internal set; } = 1.0f;
        public float MaxTrauma { get; internal set; } = 1.0f;
        public float BaseFOV { get; internal set; } = 60f;
        public float MaxFOV { get; internal set; } = 90f;
        public float FovSpeedThreshold { get; internal set; } = 8f;
        public float FovMaxSpeed { get; internal set; } = 12f;
        public float FovLerpSpeed { get; internal set; } = 0.1f;
        public CameraConstraint Constraints { get; internal set; } = CameraConstraint.Default();
        private int _splitscreenPlayers;
        private string _splitscreenMode;
        private List<(string Id, CameraRig Rig)> _splitscreenRigs;
        public IReadOnlyList<(string Id, CameraRig Rig)> SplitscreenRigs => _splitscreenRigs;
        public int SplitscreenPlayers => _splitscreenPlayers;
        private CameraRig _minimapCamera;
        private const float MinimapHeight = 50f;
        public CameraRig MinimapCamera => _minimapCamera;

        public CameraRig(Vector3 startPosition, Vector3 targetPosition)
        {
            Position = startPosition; TargetPosition = targetPosition;
            Rotation = new Vector3(0f, 0f, 0f); FOV = 60f;
            _splitscreenRigs = new List<(string, CameraRig)>();
            _minimapCamera = new CameraRig(startPosition, targetPosition, true) { Mode = CameraMode.Minimap };
        }

        // Internal constructor for minimap camera to avoid infinite recursion
        private CameraRig(Vector3 startPosition, Vector3 targetPosition, bool isMinimap)
        {
            Position = startPosition; TargetPosition = targetPosition;
            Rotation = new Vector3(0f, 0f, 0f); FOV = 60f;
            _splitscreenRigs = new List<(string, CameraRig)>();
            if (!isMinimap)
                _minimapCamera = new CameraRig(startPosition, targetPosition, true) { Mode = CameraMode.Minimap };
        }

        public void UpdateThirdPersonFollow(float dt)
        {
            Vector3 desiredPos = TargetPosition + new Vector3(OffsetsRight, OffsetsUp, -OffsetsBack);
            float factor = 1f - (float)Math.Exp(-Damping * dt);
            Position = Vector3.Lerp(Position, desiredPos, factor);
            Rotation = ComputeLookAt(TargetPosition);
        }

        public bool CollisionCorrect(float dt, List<Vector3> obstacles, float collisionOffsetAhead = 0.2f)
        {
            Vector3 from = Position, to = TargetPosition;
            Vector3 dir = to - from;
            float dist = dir.Length();
            if (dist < 0.001f) return false;
            dir = dir * (1f / dist);
            foreach (var obs in obstacles)
            {
                Vector3 toObs = obs - from;
                float t = Vector3.Dot(toObs, dir);
                if (t < 0f || t > dist) continue;
                Vector3 closest = from + dir * t;
                if ((closest - obs).Length() < 1.0f)
                {
                    Position = from + dir * (t + collisionOffsetAhead);
                    return true;
                }
            }
            return false;
        }

        public void SwitchMode(CameraMode newMode, float duration = 1.5f)
        {
            _modeStartPos = Position; _modeStartRot = Rotation;
            _modeTransitionDuration = duration; _modeTransitionElapsed = 0f; _modeTransitionActive = true;
            if (newMode == CameraMode.Fixed) { _modeEndPos = new Vector3(0f, 5f, -10f); _modeEndRot = new Vector3(-30f, 0f, 0f); }
            else if (newMode == CameraMode.ThirdPersonFollow) { _modeEndPos = TargetPosition + new Vector3(OffsetsRight, OffsetsUp, -OffsetsBack); _modeEndRot = ComputeLookAt(TargetPosition); }
            Mode = newMode;
        }

        public void UpdateModeTransition(float dt)
        {
            if (!_modeTransitionActive) return;
            _modeTransitionElapsed += dt;
            float t = Math.Min(1f, _modeTransitionElapsed / _modeTransitionDuration);
            float eased = t * t * (3f - 2f * t);
            Position = Vector3.Lerp(_modeStartPos, _modeEndPos, eased);
            Rotation = Vector3.Lerp(_modeStartRot, _modeEndRot, eased);
            if (t >= 1f) { _modeTransitionActive = false; _modeTransitionElapsed = 0f; }
        }

        public void AddTrauma(float traumaIncrement, float distanceToSource, float closeDistance = 5f)
        {
            float scale = closeDistance / Math.Max(distanceToSource, 0.1f);
            _trauma = Math.Min(MaxTrauma, _trauma + traumaIncrement * Math.Min(1f, scale));
        }

        public Vector3 ApplyTraumaShake(float dt, float time)
        {
            _trauma = Math.Max(0f, _trauma - TraumaDecayRate * dt);
            if (_trauma <= 0f) return Vector3.Zero;
            float m = _trauma * _trauma;
            return new Vector3((float)Math.Sin(time * 7.3f) * m * 0.05f, (float)Math.Sin(time * 11.7f) * m * 0.03f, (float)Math.Sin(time * 17.1f) * m * 0.04f);
        }

        public void UpdateFOV(float speed)
        {
            float target = BaseFOV;
            if (speed > FovSpeedThreshold) target = BaseFOV + (MaxFOV - BaseFOV) * Math.Min(1f, (speed - FovSpeedThreshold) / (FovMaxSpeed - FovSpeedThreshold));
            FOV = FOV + (target - FOV) * FovLerpSpeed;
        }

        public void ApplyInput(float deltaPitchDeg, float deltaYawDeg)
        {
            float np = Math.Max(Constraints.PitchMinDeg, Math.Min(Constraints.PitchMaxDeg, Rotation.X + deltaPitchDeg));
            float ny = Rotation.Y;
            if (!Constraints.YawUnlimited) ny = Math.Max(Constraints.YawMinDeg, Math.Min(Constraints.YawMaxDeg, Rotation.Y + deltaYawDeg));
            else ny += deltaYawDeg;
            Rotation = new Vector3(np, ny, Rotation.Z);
        }

        public void SetupSplitscreen(int playerCount, string mode = "horizontal")
        {
            _splitscreenPlayers = playerCount; _splitscreenMode = mode; _splitscreenRigs.Clear();
            for (int i = 0; i < playerCount; i++)
                _splitscreenRigs.Add(($"player_{i + 1}", new CameraRig(Vector3.Zero, TargetPosition) { Damping = Damping, OffsetsRight = OffsetsRight, OffsetsBack = OffsetsBack, OffsetsUp = OffsetsUp }));
        }

        public (float x, float y, float w, float h) GetViewportRect(int playerIndex)
        {
            if (_splitscreenMode == "horizontal")
            {
                float hH = 1f / _splitscreenPlayers;
                return (0f, (1 - hH) * playerIndex, 1f, hH);
            }
            float wH = 1f / _splitscreenPlayers;
            return (wH * playerIndex, 0f, wH, 1f);
        }

        public void UpdateMinimap(Vector3 playerPos)
        {
            if (_minimapCamera != null)
                _minimapCamera.Position = new Vector3(playerPos.X, MinimapHeight, playerPos.Z);
        }

        public void Update(float dt, List<Vector3> obstacles = null)
        {
            if (Mode == CameraMode.Fixed) { UpdateModeTransition(dt); return; }
            UpdateThirdPersonFollow(dt);
            UpdateModeTransition(dt);
            if (obstacles != null) CollisionCorrect(dt, obstacles);
        }

        private Vector3 ComputeLookAt(Vector3 target)
        {
            Vector3 dir = target - Position;
            float yaw = (float)Math.Atan2(dir.X, dir.Z);
            float pitch = (float)Math.Atan2(-dir.Y, Math.Sqrt(dir.X * dir.X + dir.Z * dir.Z));
            return new Vector3(pitch * (180f / (float)Math.PI), yaw * (180f / (float)Math.PI), 0f);
        }
    }

    public static class Vector3Extensions
    {
        public static float Length(this Vector3 v) => (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
    }
}
