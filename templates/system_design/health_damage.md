# 生命伤害系统设计（health_damage）— T-SYS-006

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的伤害结算层。唯一有权修改实体生命值的模块。
- 边界内：伤害计算（部位加成/护甲）、生命值修改、死亡检测、死亡事件广播、掉落表结算。
- 边界外：武器射击（weapon_system）、弹体碰撞（projectile_system）、敌人 AI（enemy_ai）、刷兵系统；本系统只消费"命中事件"，不感知上游。
- factory_gate 对应项：`health_damage — 命中判定+伤害计算+死亡可触发`（templates/game_capabilities.yaml）。

## 验收标准对照
- [ ] 伤害只经 health_damage 修改生命值，代码审查无旁路写入
- [ ] 伤害公式：baseDamage × partMultiplier − armor，下限 0
- [ ] 死亡事件触发后掉落、积分、刷兵计数均正确联动
- [ ] Core 层 `dotnet build` + `dotnet test` 独立通过，无 UnityEngine 依赖
- [ ] 输入链路 HitEvent → HealthDamageSystem(process) → DeathEvent 单向贯通

## 分层设计
- `Contra3D.Core`（纯逻辑层，无 UnityEngine 引用）：
  - `HealthDamageSystem`：核心处理器。`ProcessHit(HitEvent hit) → HealthChange`。
  - `HealthComponent`：实体生命值组件（current/max/armor/partMultipliers）。
  - `DeathEvent`：`EntityId`、`KillerId`、`DropTableId`。
  - `HealthChangeEvent`：`EntityId`、`DamageDealt`、`NewHealth`、`IsDead`。
  - 已有：`DamageCalculator.Calculate(baseDamage, partMultiplier, armor)`（纯函数，确定性）。
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `HealthManager`：每帧订阅 HitEvent，调用 HealthDamageSystem，广播 HealthChangeEvent/DeathEvent。
  - `DropManager`：订阅 DeathEvent，按 drop_table 结算掉落。

## 文件布局
```
contra_3d/
├── Assets/Scripts/
│   ├── Core/
│   │   ├── HealthDamageSystem.cs   # 伤害处理 + 死亡检测
│   │   ├── HealthComponent.cs      # 生命值组件
│   │   └── HealthEvents.cs         # HealthChangeEvent / DeathEvent
│   └── Runtime/
│       ├── HealthManager.cs        # MonoBehaviour: 事件桥接
│       └── DropManager.cs          # 掉落结算
├── src/                             # 镜像 Assets/Scripts/
└── tests/Core.Tests/HealthDamageTests.cs   # dotnet test
```
- asmdef 沿用既有：`Contra3D.Core.asmdef` + `Contra3D.Runtime.asmdef`。
- 已有文件：`src/Core/DamageCalculator.cs`（不修改）。

## 数据流
```
ProjectileSystem → HitEvent (damage, targetId, hitPoint)
  → HealthDamageSystem.ProcessHit(hit)
  → DamageCalculator.Calculate(hit.damage, partMultiplier, armor)
  → HealthComponent.current -= result
  → if current <= 0: DeathEvent broadcast
  → if current > 0: HealthChangeEvent broadcast
```
- 禁止回边：Core 不感知 UnityEngine / EventSystem；Runtime 只绑定与转发。

## 关键数值约束
- 伤害下限：0（不产生负伤害）。
- 护甲减免：`max(0, damage × partMultiplier - armor)`。
- 死亡阈值：health ≤ 0。
- 无敌帧：死亡后 1.0s 内免疫再次伤害（防止连击 bug）。
- 掉落结算：按 enemies.yaml 的 drop_table 概率表，独立随机roll。

## 测试策略（Core 层 dotnet 单测）
- `HealthDamageTests`（朴素断言，`dotnet test` 运行）：
  1. 基础伤害计算：damage=12, partMultiplier=1.0, armor=0 → result=12。
  2. 头部暴击：damage=12, partMultiplier=2.0, armor=0 → result=24。
  3. 护甲减免：damage=30, partMultiplier=1.0, armor=20 → result=10。
  4. 护甲过厚：damage=10, partMultiplier=1.0, armor=20 → result=0（不下限）。
  5. 生命值递减：初始 100，受伤害 30 → 剩余 70。
  6. 死亡事件：初始 24，受伤害 24 → IsDead=true, DeathEvent 广播。
  7. 过量伤害：初始 24，受伤害 100 → Health=0, IsDead=true（不产生负生命）。
  8. 无敌帧：死亡后 0.5s 内再次受伤 → 无变化。
  9. 确定性：同 HitEvent 序列两次处理输出相同。
  10. 多目标：两个敌人各受不同伤害，独立结算。

## 验收标准清单（对应 factory_gate health_damage）
- [ ] 伤害公式正确（base × part − armor，下限 0）
- [ ] 死亡事件正确广播，无旁路生命值修改
- [ ] `dotnet build` Core 通过、`dotnet test` 全绿（上表 10 例）
- [ ] asmdef 无回边：Core 不引用 Runtime/Unity，Runtime 单向引用 Core
- [ ] 单文件 ≤ 500 行（max_file_lines）
