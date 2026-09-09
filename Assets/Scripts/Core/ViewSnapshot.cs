using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    public readonly struct ProjectileViewData
    {
        public string Id { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public bool IsActive { get; }

        public ProjectileViewData(string id, Vector3 position, Vector3 velocity, bool isActive)
        {
            Id = id; Position = position; Velocity = velocity; IsActive = isActive;
        }
    }

    public readonly struct EnemyViewData
    {
        public string Id { get; }
        public Vector3 Position { get; }
        public float YawDeg { get; }
        public string StateName { get; }
        public bool IsActive { get; }

        public EnemyViewData(string id, Vector3 position, float yawDeg, string stateName, bool isActive)
        {
            Id = id; Position = position; YawDeg = yawDeg; StateName = stateName; IsActive = isActive;
        }
    }

    /// <summary>Core→Runtime 的每帧表现快照；只读下发。</summary>
    public readonly struct ViewSnapshot
    {
        public IReadOnlyList<ProjectileViewData> Projectiles { get; }
        public IReadOnlyList<EnemyViewData> Enemies { get; }

        public ViewSnapshot(IReadOnlyList<ProjectileViewData> projectiles, IReadOnlyList<EnemyViewData> enemies)
        {
            Projectiles = projectiles;
            Enemies = enemies;
        }
    }
}
