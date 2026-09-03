# 弹体系统设计（projectile_system）— T-SYS-004

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的弹体管理层。负责弹体生命周期（生成/推进/回收）、弹道类型分派（直线/散射/抛物线）、碰撞检测容差、同屏上限管理。
- 边界内：弹体对象池、弹道推进、碰撞检测（容差扫掠）、超时/出界回收、阵营信息（敌我识别）。
- 边界外：武器状态（weapon_system）、伤害结算（health_damage）、敌人 AI（enemy_ai）；本系统只产出"命中事件"。
- factory_gate 对应项：`projectile_system — 弹体生命周期+碰撞判定可触发`（templates/game_capabilities.yaml）。

## 验收标准对照
- [ ] 直线/散射/抛物线三种弹道均表现正确，弹体超时与出界正常回收
- [ ] 高速弹体（≥ 30 m/s）不发生可见穿透（容差扫掠生效）
- [ ] 同屏弹体 ≤ 200，超出时优先回收最旧弹体
- [ ] Core 层 `dotnet build` + `dotnet test` 独立通过，无 UnityEngine 依赖
- [ ] 输入链路 FireEvent → ProjectileSystem(spawn) → HitEvent 单向贯通

## 分层设计
- `Contra3D.Core`（纯逻辑层，无 UnityEngine 引用）：
  - `ProjectileSystem`：核心管理器。`SpawnProjectile(FireEvent evt, Vector3 origin, Vector3 direction) → ProjectileId`。
  - `ProjectileDefinition`：弹体参数（速度、半径、damage、ownerTag、lifeTime）。
  - `ProjectileState`：位置、速度、出生时间、是否命中。
  - `HitEvent`：`ProjectileId`、`TargetId`、`Damage`、`HitPoint`。
  - 对象池：预分配 N 个 ProjectileState，用 free list 管理。
  - 弹道类型：
    - `Hitscan`：瞬时命中检测（射线投射），不生成物理弹体，直接产生态命中事件。
    - `Projectile`：物理弹体，每帧推进位置，检测碰撞。
    - `Homing`：追踪弹体，每帧转向目标。
  - 碰撞检测：AABB/球体相交，带容差扩径（1.0–1.5× 单帧位移）。
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `ProjectileManager`：每帧调用 `ProjectileSystem.Update(dt)`，处理弹体推进和碰撞。
  - `ProjectileRenderer`：实例化弹体视觉效果（球体/线条），由 ProjectileSystem 提供位置数据。

## 文件布局
```
contra_3d/
├── Assets/Scripts/
│   ├── Core/
│   │   ├── ProjectileSystem.cs      # 对象池 + 弹道推进 + 碰撞检测
│   │   ├── ProjectileTypes.cs       # ProjectileState / HitEvent / ProjectileDefinition
│   │   └── HitscanDetector.cs       # 射线检测工具（纯数学）
│   └── Runtime/
│       ├── ProjectileManager.cs     # MonoBehaviour: 每帧更新
│       └── ProjectileRenderer.cs    # 视觉效果
├── src/                             # 镜像 Assets/Scripts/
└── tests/Core.Tests/ProjectileSystemTests.cs   # dotnet test
```
- asmdef 沿用既有：`Contra3D.Core.asmdef` + `Contra3D.Runtime.asmdef`。

## 数据流
```
WeaponSystem.ProcessFireRequest() → FireEvent
  → ProjectileSystem.SpawnProjectile(fireEvent, origin, direction)
  → [hitscan] 立即射线检测 → HitEvent
  → [projectile] 加入对象池，每帧推进
  → ProjectileSystem.Update(dt):
      ├── 推进弹体位置
      ├── 碰撞检测（容差扫掠）
      │   └── 命中 → HitEvent 广播给 health_damage
      ├── 超时检查 → 回收
      └── 出界检查 → 回收
```
- 禁止回边：Core 不感知 UnityEngine / Physics / Collider；Runtime 只绑定与转发。

## 关键数值约束
- 同屏弹体上限：200（硬编码常量 MAX_PROJECTILES）。
- 碰撞容差：弹体半径 + max(1.0× 单帧位移, 0.1m)（防高速穿模）。
- 弹体寿命：默认 5.0s（可配置），超时自动回收。
- 出界检测：距离出生点 > 500m 自动回收。
- hitscan 射线：最大距离 500m，穿透多层目标（第一个命中即止）。
- 追踪弹转向速率：5.0 rad/s（硬编码常量 HOMING_TURN_RATE）。

## 测试策略（Core 层 dotnet 单测）
- `ProjectileSystemTests`（朴素断言，`dotnet test` 运行）：
  1. 对象池初始化：创建 10 个弹体槽位，free count = 10。
  2. 弹体生成：Spawn 后 free count 递减，active count 递增。
  3. 弹体回收：标记命中后回收，free count 恢复。
  4. 同屏上限：生成 201 个弹体，第 201 个被拒绝（返回 invalid id）。
  5. 超时回收：生成弹体，推进 5.1s，自动回收。
  6. 出界回收：生成弹体，推进 501m 距离，自动回收。
  7. hitscan 命中：射线检测到目标 → 产出 HitEvent。
  8. hitscan 未命中：射线未检测到目标 → 无 HitEvent。
  9. 碰撞容差：高速弹体（100 m/s）穿过薄墙（0.05m）→ 容差扫掠命中。
  10. 确定性：同输入序列两次 Spawn+Update 输出 HitEvent 序列一致。

## 验收标准清单（对应 factory_gate projectile_system）
- [ ] 直线/散射/抛物线三种弹道均正确分派（Hitscan vs Projectile vs Homing）
- [ ] 同屏 200 弹体上限生效，超出拒绝生成
- [ ] 高速弹体容差扫掠生效（1.0–1.5× 单帧位移扩径）
- [ ] `dotnet build` Core 通过、`dotnet test` 全绿（上表 10 例）
- [ ] asmdef 无回边：Core 不引用 Runtime/Unity，Runtime 单向引用 Core
- [ ] 单文件 ≤ 500 行（max_file_lines）
