# 地图加载系统设计（map_loading）— T-SYS-007

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的关卡数据加载层。纯逻辑层，零 UnityEngine 引用。
- 职责：加载 YAML 地图定义 → 验证引用完整性 → 构建 Core 可用场景数据 → 广播 SceneLoadedEvent。
- 边界内：YAML 解析、引用校验、SceneData 构造、SpawnPoint 列表、CoverPoint 列表、Pickup 注册表。
- 边界外：3D 渲染（rendering）、碰撞体生成（collision）、实体 instantiation（Runtime 层）。

## 验收标准
- [ ] data/maps/maps.yaml 中的每个 map 引用 valid weapon/enemy/pickup schema
- [ ] 所有 SpawnPoint 坐标 (x,y,z) 在碰撞边界内（collision_bound_x）
- [ ] CoverPoint 存在且不与 SpawnPoint 重叠
- [ ] Pickup locations 有效且不超出地图边界
- [ ] Core 编译通过，dotnet test 全部通过

## 分层设计
- `Contra3D.Core`（纯逻辑层）：
  - `MapDefinition`：不可变数据模型，包含 map_id/name/size/enemies/pickups/spawn_points/cover_points
  - `MapLoader`：从 YAML 字符串加载 MapDefinition，执行引用校验
  - `MapValidationError`：校验失败时返回结构化错误列表
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `MapManager`：持 MapLoader 实例，每帧更新，订阅 SceneLoadedEvent

## 文件布局
```
contra_3d/
├── src/
│   ├── Core/
│   │   ├── MapDefinition.cs       # 数据模型
│   │   ├── MapLoader.cs           # YAML 加载 + 校验
│   │   └── MapTypes.cs            # SpawnPoint, CoverPoint, PickupLocation, MapValidationError
│   └── Tests/Contra3D.Core.Tests/
│       └── MapLoaderTests.cs      # 单元测试
├── data/maps/maps.yaml            # 现有地图数据
└── _os_state/design_contracts/map_loading.bdd.yaml
```

## 数据流
```
data/maps/maps.yaml
  → MapLoader.Load(path) → MapDefinition | List<MapValidationError>
  → MapDefinition → SceneLoadedEvent (Runtime 层广播)
```

## 关键数值约束
- 同地图 SpawnPoint 数量 ≥ 2（玩家 + 敌人各有出生点）
- SpawnPoint 间距 ≥ 5m（防原地刷怪）
- CoverPoint 数量 ≥ SpawnPoint 数量的 50%
- Pickup 总数 ≤ 20 per map（防过载）

## 测试策略
- MapLoaderTests：加载有效 YAML → 解析成功；加载无效引用 → 返回错误；边界值校验
