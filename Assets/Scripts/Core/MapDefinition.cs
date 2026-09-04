// T-SYS-007 (map_loading/Core) — 地图定义数据模型。
// 设计来源: templates/system_design/map_loading.md（Core 纯逻辑层，零 UnityEngine 依赖）。
// 镜像规则: 与 Assets/Scripts/Core/MapDefinition.cs 内容一一对应。

using System;

namespace Contra3D.Core
{
    /// <summary>
    /// 地图场景数据模型（不可变）。由 <see cref="MapLoader"/> 加载并校验后产出。
    /// </summary>
    public sealed class MapDefinition
    {
        /// <summary>地图唯一标识（如 "m_stage01"）。</summary>
        public string MapId { get; }

        /// <summary>地图显示名称。</summary>
        public string Name { get; }

        /// <summary>出生点列表（不可空，长度 ≥ 2）。</summary>
        public SpawnPoint[] SpawnPoints { get; }

        /// <summary>掩体点列表。</summary>
        public CoverPoint[] CoverPoints { get; }

        /// <summary>拾取点列表。</summary>
        public PickupLocation[] PickupLocations { get; }

        /// <summary>碰撞边界 X 半宽（米），默认 25.0。</summary>
        public float CollisionBoundX { get; }

        /// <summary>
        /// 创建地图定义。所有引用均为不可变副本。
        /// </summary>
        public MapDefinition(
            string mapId,
            string name,
            SpawnPoint[] spawnPoints,
            CoverPoint[] coverPoints,
            PickupLocation[] pickupLocations,
            float collisionBoundX = 25.0f)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                throw new ArgumentException("MapId must not be null or whitespace.", nameof(mapId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
            if (spawnPoints == null || spawnPoints.Length < 2)
                throw new ArgumentException("SpawnPoints must contain at least 2 entries.", nameof(spawnPoints));
            if (coverPoints == null)
                throw new ArgumentException("CoverPoints must not be null.", nameof(coverPoints));
            if (pickupLocations == null)
                throw new ArgumentException("PickupLocations must not be null.", nameof(pickupLocations));

            MapId = mapId;
            Name = name;
            SpawnPoints = (SpawnPoint[])spawnPoints.Clone();
            CoverPoints = (CoverPoint[])coverPoints.Clone();
            PickupLocations = (PickupLocation[])pickupLocations.Clone();
            CollisionBoundX = collisionBoundX;
        }
    }
}
