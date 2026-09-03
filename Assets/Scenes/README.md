# Assets/Scenes/ — 场景说明（T-SYS-001）

## Boot.unity（唯一入口场景 — 尚未创建）

Unity Editor 未安装，本目录仅含本说明文件。**场景为二进制/YAML 资产，禁止由 OS 脚本凭空生成**，需在 Editor 内手动创建。

### 手动创建步骤（Unity Hub 安装 6000.0 LTS Editor 后）

1. Unity Hub → Add → 选择 `F:\AI_Projects\projects\contra_3d`（用 Unity 6000.0.xxf1 打开，首次会重写 ProjectVersion.txt 的精确补丁号并生成 packages-lock.json，需人工核对 diff）。
2. `File → New Scene`，模板选 **URP**（Basic (URP) 或 Standard (URP)），保存为 `Assets/Scenes/Boot.unity`。
3. 按设计契约搭建场景组成：
   - 主相机：Perspective，FOV 60°（对齐 `CameraParams.FovDeg = 60.0`）。
   - 方向光：URP Directional Light。
   - 测试几何体：1 块地面 Plane + 3–5 个 Cubes（掩体/靶子），材质使用 `URP/Lit` 默认资源，不依赖外部资产。
   - 准星 UI：Screen Space Canvas + 十字线（4 条 Line/Image），挂载 `CrosshairUI`（Contra3D.Runtime 程序集）。
4. 在空 GameObject 上挂载 `GameBootstrap` 与 `CameraRigBootstrap`（Runtime 层引导脚本）。
5. `File → Build Profiles`（Unity 6）→ 将 `Boot.unity` 加入 Scene List，设为第 0 位（唯一入口场景）。
6. PlayMode 冒烟验证（rendering.yaml playmode_min）：进入 Play Mode，断言相机/方向光/测试几何体/准星 Canvas 存在。**此项依赖 Editor，OS 侧无法自动化，留待 Editor 就绪后执行。**

### 依赖约束

- Boot 场景仅使用 URP/Lit 默认资源（invariant: 不依赖外部资产）。
- 输入链路走 Input System 包（manifest 已声明 `com.unity.inputsystem`）；首次打开如提示启用新 Input System 后端，选择 Yes（Active Input Handling = Input System Package）。
