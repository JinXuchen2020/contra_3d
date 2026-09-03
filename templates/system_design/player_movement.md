# 玩家移动系统设计（player_movement）— T-SYS-002

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的玩家移动层。负责 WASD 平面移动、鼠标视角旋转（Input System）、跳跃（含 coyote time 与输入缓冲）。
- 边界内：移动向量计算、跳跃状态机、coyote/buffer 计时逻辑、输入桥接、CharacterController 驱动。
- 边界外：武器/射击逻辑、生命值、动画状态机、敌人 AI（分属 weapon/combat/ai 系统）；本系统只输出位移与朝向。
- factory_gate 对应项：`player_movement — WASD 移动 + 鼠标视角 + 跳跃可玩`（templates/game_capabilities.yaml）。

## 验收标准对照
- [ ] WASD 相对相机水平面移动，斜向输入归一化（对角线速度 = 直线速度）
- [ ] 鼠标视角经 Input System `Look` Action（Mouse Delta）驱动 yaw/pitch，无硬编码键位
- [ ] 跳跃支持 coyote time（离地后 80–150ms 内仍可跳，取默认 120ms）
- [ ] 跳跃支持输入缓冲（落地前 100–200ms 按跳，落地瞬间执行，取默认 150ms）
- [ ] Core 层 `dotnet build` + `dotnet test` 独立通过，无 UnityEngine 依赖
- [ ] 输入链路 Input → PlayerMotor(Simulate) → CharacterController.Move 单向贯通

## 分层设计
- `Contra3D.Core`（纯逻辑层，无 UnityEngine 引用）：
  - `PlayerMotor`：核心模拟器。`Simulate(MotorInput input, float dt) → MotorState`。
  - `MotorInput`：`Vector2 MoveXZ`、`Vector2 LookDelta`、`bool JumpPressed`（按下事件，含时间戳）、`bool IsGrounded`（由 Runtime 回填）。
  - `MotorState`：`Vector3 Position`、`float Yaw/Pitch`、`Vector3 Velocity`、`GroundedState`。
  - 跳跃状态机：`Grounded → Ascending → Falling → Grounded`；coyote 与 buffer 计时器在 Core 内以 dt 累计（帧率无关，不用帧计数）。
- `Contra3D.Runtime`（MonoBehaviour 层）：
  - `PlayerInputAdapter`：持有 `GameInputActions`，轮询 `Move/Look`，`Jump.performed` 事件打时间戳（对齐 unity_input.md 第 3/4 节模式）。
  - `PlayerController`：每 `FixedUpdate` 组装 `MotorInput` → 调用 `PlayerMotor.Simulate` → `CharacterController.Move(state.Velocity * dt)`；视角增量直接驱动 transform 旋转。
  - CharacterController 选型依据：地形/墙体走 Static Collider + Move 碰撞解算，玩家不做刚体动力学（对齐 unity_physics.md 角色驱动约定）。

## 文件布局（目录统一决策：assets/ → Assets/）
```
contra_3d/
├── Assets/Scripts/
│   ├── Core/PlayerMotor.cs            # 纯逻辑：移动/跳跃/coyote/buffer
│   ├── Core/MotorTypes.cs             # MotorInput / MotorState / 常量
│   └── Runtime/
│       ├── PlayerInputAdapter.cs      # Input System 桥接
│       └── PlayerController.cs        # CharacterController 驱动
├── src/                               # 镜像 Assets/Scripts/（OS 工具扫描 src/）
│   ├── Core/
│   └── Runtime/
└── tests/Core.Tests/PlayerMotorTests.cs   # dotnet test（朴素断言）
```
- 本任务执行目录统一：既有 `assets/` 目录重命名为 `Assets/`（Unity 规范大小写），并更新渲染系统文档中的路径引用与镜像同步脚本。
- asmdef 沿用 T-SYS-001：`Contra3D.Core.asmdef`（autoReferenced: false，无 Unity 依赖）、`Contra3D.Runtime.asmdef`（引用 Core）。

## 数据流
```
Input System (GameInputActions)
  → PlayerInputAdapter（轮询 Move/Look + Jump 时间戳事件）
  → PlayerController.FixedUpdate：组装 MotorInput（含 CharacterController.isGrounded 回填）
  → PlayerMotor.Simulate(input, dt) → MotorState（纯函数，确定性）
  → CharacterController.Move(state.Velocity * dt) + transform 旋转
```
- 禁止回边：Core 不感知 CharacterController/Transform；Runtime 只绑定与转发，不含游戏规则。

## 量化参数表（摘自 shooter_base.yaml game_feel / movement_feel）
| 参数 | 取值 | 依据 |
|------|------|------|
| max_speed | 7 m/s（范围 5–10） | movement_feel.acceleration_curve |
| ground_accel | 40 m/s²（范围 20–60） | 同上 |
| ground_decel | 60 m/s²（范围 30–100，≈1.5× 加速 → 停得干脆） | 同上 |
| turn_response | 1 帧（范围 0–3） | 同上 |
| air_control 系数 | 0.6（范围 0.4–0.8，空中加速度 = 地面 × 系数） | movement_feel.air_control |
| jump height | 2.2 m（范围 1.5–3.0） | movement_feel.jump_parameters |
| airtime | 0.85 s（范围 0.6–1.2） | 同上 |
| gravity_scale | 2.2（范围 1.5–3.0，快升快降） | 同上 |
| fall_gravity_mult | 1.5（范围 1.2–2.0，下落重力 > 上升 → 干脆抛物线） | 同上 |
| variable_jump | 松键截断上升速度至 30–50% | 同上 |
| coyote_time | 120 ms（范围 80–150） | input_response.coyote_time |
| input_buffer | 150 ms（范围 100–200） | input_response.input_buffer |

- 全部参数集中为 Core 常量/配置结构（`MotorConfig`），允许运行时调参，禁止散落魔法数。
- 初始速度由 `v = 2h/t_up`、`t_up = airtime/2`（上升段）反解，与 gravity_scale 联动校验。

## 测试策略（Core 层 dotnet 单测）
- `PlayerMotorTests`（朴素断言，`dotnet test` 运行，不依赖 Unity Test Framework）：
  1. 移动向量归一化：输入 (1,1) 速度模长 = max_speed（斜向不超速）。
  2. 零输入减速：ground_decel 曲线下速度递减至 0，不减为负。
  3. 加速收敛：持续满输入在预期时间收敛至 max_speed（±5%）。
  4. 空中控制：空中加速度 = 地面 × 0.6（系数边界 0.4/0.8 亦验证）。
  5. coyote 上边界：离地后 119ms 按跳 → 起跳成功；150ms → 失败（默认 120ms，边界取范围端点各测）。
  6. buffer 上边界：落地前 100ms 按跳 → 落地瞬间执行；200ms 前 → 丢弃。
  7. buffer 消费清空：缓冲命中执行后不再二次触发。
  8. variable_jump：上升中松键速度截断至 30–50% 区间。
  9. fall_gravity_mult：下落加速度 = 重力 × 1.5。
  10. 确定性：同输入序列两次 Simulate 输出逐帧一致（浮点容差 1e-4）。
- Runtime 层仅 PlayMode 冒烟：WASD 驱动位移、跳跃离地、落地回弹（不计入覆盖率门槛）。

## 验收标准清单（对应 factory_gate player_movement）
- [ ] WASD 移动 + 斜向归一化 + 鼠标视角（Input System，无硬编码键位）
- [ ] 跳跃含 coyote time 120ms 与输入缓冲 150ms（参数可配置，数值出自 shooter_base.yaml）
- [ ] `dotnet build` Core 通过、`dotnet test` 全绿（上表 10 例）
- [ ] assets/ → Assets/ 重命名完成，镜像同步规则与校验更新
- [ ] asmdef 无回边：Core 不引用 Runtime/Unity，Runtime 单向引用 Core
- [ ] 单文件 ≤ 500 行（max_file_lines）
