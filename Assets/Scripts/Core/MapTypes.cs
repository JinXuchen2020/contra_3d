// T-SYS-007 (map_loading/Core) — 地图类型定义。
// 设计来源: templates/system_design/map_loading.md（Core 纯逻辑层，零 UnityEngine 依赖）。
// 镜像规则: 与 Assets/Scripts/Core/MapTypes.cs 内容一一对应。

using System;

namespace Contra3D.Core
{
    /// <summary>出生队伍。</summary>
    public enum SpawnTeam
    {
        /// <summary>玩家方。</summary>
        Player,

        /// <summary>敌对方。</summary>
        Enemy
    }

    /// <summary>拾取物类型。</summary>
    public enum PickupType
    {
        /// <summary>武器。</summary>
        Weapon,

        /// <summary>生命恢复。</summary>
        Health,

        /// <summary>弹药。</summary>
        Ammo
    }

    /// <summary>出生点类型。</summary>
    public enum SpawnType
    {
        /// <summary>巡逻 — 无需 trigger。</summary>
        Patrol,

        /// <summary>伏击 — 必须有 trigger。</summary>
        Ambush,

        /// <summary>触发激活 — 必须有 trigger。</summary>
        Trigger,

        /// <summary>增援 — 必须有 trigger。</summary>
        Reinforce
    }

    /// <summary>
    /// 地图出生点。不可变值对象。
    /// </summary>
    public readonly struct SpawnPoint
    {
        /// <summary>X 坐标（米）。</summary>
        public float X { get; }

        /// <summary>Y 坐标（米，高度）。</summary>
        public float Y { get; }

        /// <summary>Z 坐标（米）。</summary>
        public float Z { get; }

        /// <summary>所属队伍。</summary>
        public SpawnTeam Team { get; }

        /// <summary>出生点类型。</summary>
        public SpawnType Type { get; }

        /// <summary>触发关联 ID（如 "zone_01"）；Patrol 类型可为空。</summary>
        public string Trigger { get; }

        /// <summary>
        /// 创建出生点。失败时抛 <see cref="ArgumentException"/>。
        /// </summary>
        public SpawnPoint(float x, float y, float z, SpawnTeam team, SpawnType type = SpawnType.Patrol, string trigger = null)
        {
            X = x;
            Y = y;
            Z = z;
            Team = team;
            Type = type;
            Trigger = trigger;
        }

        /// <summary>与另一出生点的水平距离（忽略 Y）。</summary>
        public float DistanceTo(SpawnPoint other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// 掩体点。不可变值对象。
    /// </summary>
    public readonly struct CoverPoint
    {
        /// <summary>X 坐标（米）。</summary>
        public float X { get; }

        /// <summary>Y 坐标（米，高度）。</summary>
        public float Y { get; }

        /// <summary>Z 坐标（米）。</summary>
        public float Z { get; }

        /// <summary>是否设置了 facing_normal。</summary>
        public bool HasFacingNormal { get; }

        /// <summary>朝向法线 X 分量（单位向量）；HasFacingNormal 为 false 时无意义。</summary>
        public float FacingNormalX { get; }

        /// <summary>朝向法线 Y 分量（单位向量）；HasFacingNormal 为 false 时无意义。</summary>
        public float FacingNormalY { get; }

        /// <summary>朝向法线 Z 分量（单位向量）；HasFacingNormal 为 false 时无意义。</summary>
        public float FacingNormalZ { get; }

        /// <summary>
        /// 创建掩体点。
        /// </summary>
        public CoverPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            HasFacingNormal = false;
            FacingNormalX = 0f;
            FacingNormalY = 0f;
            FacingNormalZ = 0f;
        }

        /// <summary>
        /// 创建掩体点（含朝向法线）。
        /// </summary>
        public CoverPoint(float x, float y, float z, float facingNormalX, float facingNormalY, float facingNormalZ)
        {
            X = x;
            Y = y;
            Z = z;
            HasFacingNormal = true;
            FacingNormalX = facingNormalX;
            FacingNormalY = facingNormalY;
            FacingNormalZ = facingNormalZ;
        }

        /// <summary>与另一出生点的水平距离（忽略 Y）。</summary>
        public float DistanceTo(SpawnPoint other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// 拾取物落点。不可变值对象。
    /// </summary>
    public readonly struct PickupLocation
    {
        /// <summary>X 坐标（米）。</summary>
        public float X { get; }

        /// <summary>Y 坐标（米，高度）。</summary>
        public float Y { get; }

        /// <summary>Z 坐标（米）。</summary>
        public float Z { get; }

        /// <summary>拾取物类型。</summary>
        public PickupType Type { get; }

        /// <summary>引用到已定义的有效拾取物 ID（如 "p_weapon_shotgun"）。</summary>
        public string SpawnId { get; }

        /// <summary>
        /// 创建拾取点。
        /// </summary>
        public PickupLocation(float x, float y, float z, PickupType type, string spawnId = null)
        {
            X = x;
            Y = y;
            Z = z;
            Type = type;
            SpawnId = spawnId;
        }
    }

    /// <summary>
    /// 地图验证错误。携带出错字段路径与描述信息。
    /// </summary>
    public sealed class MapValidationError
    {
        /// <summary>出错字段的点分路径（如 "spawn_points[2].team"）。</summary>
        public string Path { get; }

        /// <summary>错误描述。</summary>
        public string Message { get; }

        /// <summary>
        /// 创建验证错误。
        /// </summary>
        public MapValidationError(string path, string message)
        {
            Path = path ?? throw new ArgumentException("Path must not be null.", nameof(path));
            Message = message ?? throw new ArgumentException("Message must not be null.", nameof(message));
        }

        public override string ToString()
        {
            return $"[{Path}] {Message}";
        }
    }

    /// <summary>
    /// 场景加载完成事件。由 <see cref="MapLoader"/> 在成功加载地图后广播。
    /// </summary>
    public readonly struct SceneLoadedEvent
    {
        /// <summary>成功加载的地图定义。</summary>
        public MapDefinition MapDefinition { get; }

        /// <summary>
        /// 创建场景加载完成事件。
        /// </summary>
        public SceneLoadedEvent(MapDefinition mapDefinition)
        {
            MapDefinition = mapDefinition ?? throw new ArgumentException("MapDefinition must not be null.", nameof(mapDefinition));
        }
    }
}
