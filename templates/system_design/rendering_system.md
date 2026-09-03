# 渲染系统设计 + Unity 工程自举（shooter_contra 模板）— T-SYS-001

## 系统概述与边界
- 定位：魂斗罗风格 run-and-gun 3D 射击的渲染与工程宿主层。负责 Unity 6000.0 工程自举、Boot 场景、相机/光照/准星 UI 与 MonoBehaviour 运行时绑定。
- 边界内：工程结构、asmdef 划分、Boot 场景组成、纯逻辑层编译验证、渲染表现组件（相机、准星、测试几何体）。
- 边界外：武器逻辑、弹道计算、AI 状态机、伤害数值（分属 weapon/combat/ai 系统）；渲染层只做表现与输入转发，不含游戏规则。
- factory_gate 对应项：`rendering — 3D场景可渲染 + 准星UI`（templates/game_capabilities.yaml）。

## Unity 6000.0 工程结构设计
```
contra_3d/
├── ProjectSettings/ProjectVersion.txt   # m_EditorVersion: 6000.0（版本锁定文件, 对齐 project.yaml framework.lock_file）
├── Packages/manifest.json               # 最小依赖集: com.unity.render-pipelines.universal (URP), com.unity.inputsystem, com.unity.test-framework
├── Assets/
│   ├── Scenes/Boot.unity                # 启动场景（唯一入口场景）
│   └── Scripts/
│       ├── Contra3D.Core.asmdef         # 纯逻辑层：无 UnityEngine 依赖
│       └── Contra3D.Runtime.asmdef      # MonoBehaviour 层：引用 Core
├── src/                                 # OS 侧逻辑源镜像（与 Assets/Scripts/ 保持同步）
│   ├── Core/                            # 镜像 Contra3D.Core
│   └── Runtime/                         # 镜像 Contra3D.Runtime
└── adapter/tools/                       # OS 采集脚本（collect_metrics.py 等, project.yaml 引用）
```
- 镜像规则：`Assets/Scripts/` 与 `src/` 内容一一对应；OS 工具（check_hardcoded 等）扫描 `src/`，Unity 编译消费 `Assets/Scripts/`。同步脚本随工程自举一并建立。

## asmdef 划分
- `Contra3D.Core`：纯 C# 逻辑（武器定义、弹道、状态机、伤害公式、RNG），**不引用任何 UnityEngine 程序集**；`autoReferenced: false`。
- `Contra3D.Runtime`：MonoBehaviour 层（PlayerController、CameraFollow、CrosshairUI、ProjectileView、EnemyView），引用 Core + UnityEngine 模块；只做绑定与转发，游戏规则全部下沉 Core。

## Boot 场景组成
- 主相机（Perspective, 60° FOV）+ 方向光（URP）。
- 测试几何体：1 块地面 Plane、3–5 个 Cubes 作为掩体/靶子，材质使用 URP/Lit 默认资源，不依赖外部资产。
- 准星 UI：Screen Space Canvas + 十字线（4 条 Line/Image），世界坐标锁定逻辑由 Core 的 `CrosshairSolver` 计算，Runtime 层仅驱动 RectTransform。

## 纯逻辑层 dotnet 验证策略
- `Contra3D.Core.asmdef` 不含 UnityEngine 依赖 → 可脱离 Unity 用 `dotnet build` 独立编译（对齐 project.yaml `commands.check` 的降级路径：Unity 未安装时仅编译 Core csproj）。
- Core 内所有游戏逻辑（伤害公式、弹道推进、状态机 tick）为纯函数或显式状态输入输出，保证同输入同输出确定性。
- Core 单元测试用 `dotnet test` / 朴素断言即可运行，不依赖 Test Framework 包。

## 依赖与接口契约
- Core 不依赖 Runtime；Runtime 单向依赖 Core（禁止回边，对齐 architecture_review.forbid_back_edge_in_dependency）。
- Runtime 组件每帧读取 Core 模拟状态（`SimulationState`）并同步到 GameObject；模拟推进由 Core 的 `GameSimulation.Tick(float dt, InputFrame input)` 完成。
- 输入链路：Runtime 捕获 Input System 事件 → 组装 `InputFrame` → 交给 Core；Core 不感知 GameObject/Transform。

## 数值约束
- 目标分辨率 1920×1080，目标帧率 60 FPS；同屏 200 弹体压力下帧率 ≥ 目标帧率的 90%（对齐 combat_system.md 验收）。
- 准星灵敏度：鼠标 1 count → 相机 yaw/pitch 0.1°（可调参数集中在 `RenderConfig`）。
- 相机跟随平滑系数 ≤ 0.15s 阻尼；单文件 ≤ 500 行（max_file_lines）。

## 测试策略（EditMode 优先）
- Core 逻辑全部 EditMode 测试（无需播放/场景）：伤害公式、弹道推进、准星世界坐标求解。
- Runtime 层仅冒烟：Boot 场景加载后相机/光照/准星存在性断言（PlayMode 最小集）。
- 优先级：EditMode 覆盖率 ≥ 80%（qa.test_coverage.min_percentage）；PlayMode 冒烟不计入覆盖率门槛。

## 验收标准清单（对应 factory_gate rendering）
- [ ] Unity 6000.0 工程创建成功，ProjectVersion.txt = 6000.0
- [ ] `dotnet build` 可独立编译 Contra3D.Core（无 UnityEngine 依赖）
- [ ] Boot 场景打开后 3D 场景可渲染（相机+光照+测试几何体可见）
- [ ] 准星 UI 显示且随相机朝向正确对应屏幕中心/世界目标点
- [ ] Assets/Scripts/ 与 src/ 镜像同步规则文档化并有校验手段
- [ ] asmdef 无回边：Core 不引用 Runtime/Unity，Runtime 单向引用 Core
