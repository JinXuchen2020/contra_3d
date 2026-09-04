# UI/HUD 系统设计（ui_hud）— T-SYS-008

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的 HUD 显示层。纯逻辑层，零 UnityEngine 引用。
- 职责：维护玩家 HUD 状态（生命值、命数、武器、得分），响应事件驱动更新。
- 边界内：HUDState 数据模型、HUDUpdater 逻辑、事件订阅接口。
- 边界外：UI 渲染（Unity Canvas/MonoBehaviour）、音频播放、屏幕特效。

## 验收标准
- [ ] HUDState 包含 health/lives/weapon/score 字段，只由事件驱动更新
- [ ] 低血量（<25%）触发 LowHealthFlag
- [ ] 得分跨过阈值时生成 ExtraLifeEvent
- [ ] Core 编译通过，dotnet test 全部通过

## 分层设计
- `Contra3D.Core`（纯逻辑层）：
  - `HUDState`：不可变快照，含 Health、MaxHealth、Lives、Score、CurrentWeaponId、LowHealth 标志
  - `HUDUpdater`：事件驱动的状态机。订阅 HealthChangeEvent、DeathEvent、ScoreEvent 等
  - `HUDEvent`：ScoreIncrementEvent、ExtraLifeEvent、LowHealthEvent
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `HUDManager`：持 HUDUpdater 实例，每帧读取 HUDState 驱动 UI

## 文件布局
```
contra_3d/
├── src/
│   ├── Core/
│   │   ├── HUDState.cs          # 不可变 HUD 快照
│   │   ├── HUDUpdater.cs        # 事件驱动状态更新
│   │   └── HUDEvents.cs         # ScoreIncrementEvent, ExtraLifeEvent, LowHealthEvent
│   └── Tests/Contra3D.Core.Tests/
│       └── HUDUpdaterTests.cs   # 单元测试
└── _os_state/design_contracts/ui_system.bdd.yaml
```

## 数据流
```
HealthChangeEvent → HUDUpdater.Process() → HUDState (health/lives)
DeathEvent        → HUDUpdater.Process() → HUDState (lives-1 or respawn)
ScoreIncrementEvent → HUDUpdater.Process() → HUDState (score + threshold check)
  → ExtraLifeEvent (if score crosses threshold)
```

## 关键数值约束
- 低血量阈值：CurrentHealth / MaxHealth < 0.25 → LowHealth = true
- 1UP 阈值：score >= 2000 → +1 life, 阈值递增 (2k/5k/10k)
- HUDState 不可变：每次更新返回新实例

## 测试策略
- HUDUpdaterTests：初始状态、受伤更新、死亡扣命、得分递增、阈值触发1UP、低血量标记、连续事件合并
