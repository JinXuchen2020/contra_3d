// T-SYS-007 (map_loading/Core) — 地图 YAML 加载器 + 引用校验。
// 设计来源: templates/system_design/map_loading.md（Core 纯逻辑层，零 UnityEngine 依赖）。
// 镜像规则: 与 Assets/Scripts/Core/MapLoader.Parsers.cs 配合使用（partial class）。

using System;
using System.Collections.Generic;
using System.IO;

namespace Contra3D.Core
{
/// <summary>
    /// 从 YAML 文件加载地图定义。使用简单文本解析（不引入新依赖）。
    /// 校验规则:
    ///   - SpawnPoint 数量 ≥ 2
    ///   - SpawnPoint 间距 ≥ 5 m
    ///   - CoverPoint 数量 ≥ SpawnPoint 数量的 50%
    ///   - PickupLocation 数量 ≤ 20
    ///   - 所有坐标 X 绝对值 ≤ collision_bound_x（默认 25.0）
    /// </summary>
    /// <remarks>
    /// LOAD-TIME ONLY: This class performs YAML parsing with string.Split allocations.
    /// Must only be called at startup/level load, NEVER in runtime hot paths (Update, FixedUpdate, etc.).
    /// </remarks>
    public static partial class MapLoader
    {
        /// <summary>默认碰撞边界 X 半宽（米）。</summary>
        public const float DefaultCollisionBoundX = 25.0f;

        /// <summary>SpawnPoint 最小间距（米）。</summary>
        internal const float MinSpawnDistance = 5.0f;

        /// <summary>
        /// 从 YAML 文件路径加载地图定义。
        /// </summary>
        /// <returns>校验通过返回 <see cref="MapDefinition"/>；否则抛出 <see cref="MapLoadException"/>。</returns>
        /// <exception cref="MapLoadException">校验失败时抛出，携带全部 <see cref="MapValidationError"/>。</exception>
        public static MapDefinition Load(string yamlPath)
        {
            if (!File.Exists(yamlPath))
                throw new FileNotFoundException($"Map YAML not found: {yamlPath}");

            string content = File.ReadAllText(yamlPath);
            return LoadFromString(content);
        }

        /// <summary>
        /// 从 YAML 字符串加载地图定义。
        /// </summary>
        public static MapDefinition LoadFromString(string yamlContent)
        {
            var maps = ParseMaps(yamlContent);
            if (maps.Count == 0)
                throw new MapLoadException(new List<MapValidationError>
                {
                    new MapValidationError("maps", "No map entries found in YAML.")
                });

            // 取第一张地图作为主地图加载（seed 数据通常仅一张）
            var first = maps[0];
            // Apply default collision bound if not explicitly set
            if (first.CollisionBoundX <= 0f)
                first.CollisionBoundX = DefaultCollisionBoundX;
            var errors = ValidateMap(first);
            if (errors.Count > 0)
                throw new MapLoadException(errors);

            return new MapDefinition(
                first.MapId,
                first.Name,
                first.SpawnPoints.ToArray(),
                first.CoverPoints.ToArray(),
                first.PickupLocations.ToArray(),
                first.CollisionBoundX);
        }

        /// <summary>
        /// 从 YAML 字符串加载地图定义，不抛出异常。
        /// 校验通过返回 <c>(def, null)</c>；否则返回 <c>(null, errors)</c>。
        /// </summary>
        public static (MapDefinition definition, List<MapValidationError> errors) TryLoadFromString(string yamlContent)
        {
            var maps = ParseMaps(yamlContent);
            if (maps.Count == 0)
                return (null, new List<MapValidationError>
                {
                    new MapValidationError("maps", "No map entries found in YAML.")
                });

            var first = maps[0];
            if (first.CollisionBoundX <= 0f)
                first.CollisionBoundX = DefaultCollisionBoundX;
            var errors = ValidateMap(first);
            if (errors.Count > 0)
                return (null, errors);

            var def = new MapDefinition(
                first.MapId,
                first.Name,
                first.SpawnPoints.ToArray(),
                first.CoverPoints.ToArray(),
                first.PickupLocations.ToArray(),
                first.CollisionBoundX);
            return (def, null);
        }

        #region Validation

        /// <summary>验证 ParsedMap 并返回错误列表。委托给 MapValidator 辅助类。</summary>
        internal static List<MapValidationError> ValidateMap(ParsedMap m) =>
            MapValidator.Validate(m);

        #endregion

        #region Helpers

        /// <summary>YAML 解析期间的临时地图状态（仅供 MapValidator 访问）。</summary>
        internal class ParsedMap
        {
            public string MapId;
            public string Name;
            public float CollisionBoundX;
            public readonly List<SpawnPoint> SpawnPoints = new List<SpawnPoint>();
            public readonly List<CoverPoint> CoverPoints = new List<CoverPoint>();
            public readonly List<PickupLocation> PickupLocations = new List<PickupLocation>();
        }

        /// <summary>地图加载/校验异常，携带全部验证错误。</summary>
        public sealed class MapLoadException : Exception
        {
            public List<MapValidationError> Errors { get; }

            public MapLoadException(List<MapValidationError> errors)
                : base(BuildMessage(errors))
            {
                Errors = errors ?? throw new ArgumentException("Errors must not be null.", nameof(errors));
            }

            private static string BuildMessage(List<MapValidationError> errors)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Map validation failed with the following errors:");
                foreach (var e in errors)
                    sb.AppendLine($"  - {e}");
                return sb.ToString();
            }
        }

        #endregion
    }
}
