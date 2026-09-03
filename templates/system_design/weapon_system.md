# 武器系统设计（weapon_system）— T-SYS-003

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的武器管理层。负责武器定义加载、主副槽切换、射击冷却/弹匣/换弹状态机、射击事件产出。
- 边界内：武器状态机（idle/cooldown/reloading/switching）、弹药计数、射击请求合法性校验、WeaponEvent 产出。
- 边界外：弹体飞行/碰撞（projectile_system）、伤害结算（health_damage）、敌人 AI（enemy_ai）、玩家移动（player_movement）；本系统只产出"射击事件"和"切换事件"。
- factory_gate 对应项：`weapon_system — 武器切换+射击+弹道分派可触发`（templates/game_capabilities.yaml）。

## 验收标准对照
- [ ] 玩家可在 ≥ 3 种武器间无缝切换，切换动画/冷却期间射击请求被拒绝
- [ ] 直线(hitscan)/散射(projectile)两种弹道类型均正确分派
- [ ] 射速下限 0.08s 生效（连点不触发事件风暴）
- [ ] 弹匣耗尽后拒绝射击 + 换弹计时 + 换弹完成恢复射击
- [ ] Core 层 `dotnet build` + `dotnet test` 独立通过，无 UnityEngine 依赖
- [ ] 输入链路 Input → WeaponSystem(processFireRequest) → FireEvent 单向贯通

## 分层设计
- `Contra3D.Core`（纯逻辑层，无 UnityEngine 引用）：
  - `WeaponSystem`：核心状态机。`ProcessFireRequest(WeaponSlot slot) → FireEvent | Rejected`。
  - `WeaponState`：`CurrentWeaponId`、`SecondaryWeaponId`、`Ammo[slot]`、`CooldownTimer[slot]`、`ReloadTimer[slot]`、`SwitchCooldownTimer`。
  - `WeaponDefinition`（已有：WeaponData.cs）：不可变数据模型，包含 damage/fire_rate/magazine_size/reload_time/spread/type。
  - `FireEvent`：`WeaponId`、`Origin`、`Direction`、`Damage`、`SpreadDeg`、`IsHitscan`。
  - `SwitchEvent`：`FromWeaponId`、`ToWeaponId`、`Timestamp`。
  - 状态机：`Idle → Cooldown(reject) → Reloading(reject) → Switching(reject)`；各状态对 FireRequest/SwitchRequest 的响应。
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `WeaponManager`：持有 `WeaponSystem` 实例，每帧更新计时器（`Update`），订阅 Input System 的 Fire/Switch Actions。
  - `WeaponUI`：HUD 联动（弹药数/当前武器图标），仅读取 WeaponSystem 状态，不写。
  - 切换动画由 Animator 驱动，WeaponManager 在 switching 状态时阻塞射击请求。

## 文件布局
```
contra_3d/
├── Assets/Scripts/
│   ├── Core/
│   │   ├── WeaponSystem.cs          # 状态机 + 射击请求处理
│   │   ├── WeaponTypes.cs           # WeaponState / FireEvent / SwitchEvent / 常量
│   │   └── WeaponLoader.cs          # YAML → WeaponDefinition[] 加载（复用 asset_pipeline）
│   └── Runtime/
│       ├── WeaponManager.cs         # MonoBehaviour: 计时器更新 + Input 桥接
│       └── WeaponUI.cs              # HUD: 弹药/武器图标显示
├── src/                             # 镜像 Assets/Scripts/
└── tests/Core.Tests/WeaponSystemTests.cs   # dotnet test
```
- asmdef 沿用既有：`Contra3D.Core.asmdef` + `Contra3D.Runtime.asmdef`。

## 数据流
```
Input System (Fire/Switch Actions)
  → WeaponManager.Update：累积 dt，推进 cooldown/reload/switch 计时器
  → WeaponManager.OnFirePressed：调用 WeaponSystem.ProcessFireRequest(primarySlot)
  → WeaponSystem：校验冷却+弹药 → 产出 FireEvent 或 Rejected
  → WeaponSystem.OnSwitchRequested：校验切换冷却 → 产出 SwitchEvent 或 Rejected
  → FireEvent 广播给 projectile_system / hitscan_direct
  → SwitchEvent 广播给 WeaponUI + Animation
```
- 禁止回边：Core 不感知 Input System / Animator / UI；Runtime 只绑定与转发。

## 关键数值约束
- 射速下限：同一武器两次射击间隔 ≥ 1.0/fire_rate 秒（由 WeaponDefinition.fire_rate 驱动），硬下限 0.08s（防事件风暴）。
- 弹匣管理：magazine_size=0 表示无限弹药（无弹药计数，无换弹）。
- 换弹：reload_time 秒内阻塞射击；可被" cancel_reload "中断（部分武器支持），中断后恢复原弹匣剩余量。
- 切换冷却：500ms（硬编码常量 SWITCH_COOLDOWN_S），切换期间拒绝射击。
- 死亡重置：玩家死亡时，primary=weapon_box 获得的武器→rifle_default，secondary 清空，弹匣/冷却全重置。
- DPS 平衡包络：5 把武器折算 DPS 落在 rifle_default 的 0.8–1.5x 区间（见 BDD 场景 dps_balance_envelope_across_arsenal）。

## 测试策略（Core 层 dotnet 单测）
- `WeaponSystemTests`（朴素断言，`dotnet test` 运行）：
  1. 初始状态：加载 rifle_default 后 ammo=magazine_size(30)，cooldown=0，reload=false。
  2. 射击消耗弹药：连续射击 30 发后 ammo=0，第 31 发被 Rejected。
  3. 换弹循环：ammo=0 时触发 reload，经过 reload_time(1.5s) 后 ammo=magazine_size，可恢复射击。
  4. 冷却拦截：fire_rate=7/s → 间隔 0.14s 内第二次射击被 Rejected。
  5. 射速下限：fire_rate=100/s（理论间隔 0.01s）→ 实际间隔被钳制到 0.08s。
  6. 武器切换：primary=rifle，secondary=spread_shot → Switch 后 primary=spread_shot，原 rifle 保留在 secondary。
  7. 切换冷却：切换后 500ms 内再次 Switch 被 Rejected。
  8. 换弹中射击：reload 进行中 FireRequest 被 Rejected。
  9. 无限弹药：magazine_size=0 的武器射击不消耗弹药，不触发换弹。
  10. 确定性：同输入序列两次 ProcessFireRequest 输出事件序列一致。

## 验收标准清单（对应 factory_gate weapon_system）
- [ ] 武器切换（≥ 3 把）+ 切换冷却期间射击被拒绝
- [ ] hitscan/projectile 弹道分派正确（由 FireEvent.IsHitscan 字段区分）
- [ ] 射速下限 0.08s 生效，弹匣耗尽拒绝射击
- [ ] `dotnet build` Core 通过、`dotnet test` 全绿（上表 10 例）
- [ ] asmdef 无回边：Core 不引用 Runtime/Unity，Runtime 单向引用 Core
- [ ] 单文件 ≤ 500 行（max_file_lines）
