// T-SYS-001 (rendering) — 游戏引导脚本。
// 设计来源: templates/system_design/rendering_system.md + _os_state/design_contracts/rendering.yaml
// 职责: Boot 场景入口引导 — 应用 RenderConfig 数值约束（分辨率/帧率），初始化运行时环境。
// 依赖: UnityEngine（Contra3D.Runtime 程序集）；游戏规则全部下沉 Core，本脚本仅绑定与转发。

using UnityEngine;

namespace Contra3D.Runtime
{
    /// <summary>
    /// Boot 场景引导器 — 场景加载后初始化渲染目标与全局运行时参数。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>目标帧率（rendering.yaml numeric_constraints.target_fps）。</summary>
        private const int TargetFps = 60;

        private void Awake()
        {
            Application.targetFrameRate = TargetFps;

            // TODO(PlayMode): 依赖 Unity Editor — Boot 场景加载后的存在性断言
            // （相机/方向光/测试几何体/准星 Canvas，rendering.yaml playmode_min: boot_scene_smoke）
            // 需 Editor 安装后在 Test Runner 中补齐；Editor 未安装期 OS 侧仅验证 Core 纯逻辑层。
            Debug.Log("[GameBootstrap] Boot scene initialized.");
        }
    }
}
