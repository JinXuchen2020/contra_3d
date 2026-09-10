// T-SYS-001 (rendering) — 游戏引导脚本。
// 设计来源: templates/system_design/rendering_system.md + _os_state/design_contracts/rendering.yaml
// 职责: Boot 场景入口引导 — 应用 RenderConfig 数值约束（分辨率/帧率），初始化运行时环境。
// 依赖: UnityEngine（Contra3D.Runtime 程序集）；游戏规则全部下沉 Core，本脚本仅绑定与转发。

using UnityEngine;
using Contra3D.Core;

namespace Contra3D.Runtime
{
    /// <summary>
    /// Boot 场景引导器 — 场景加载后初始化渲染目标与全局运行时参数。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var config = RenderConfig.Default;
            Application.targetFrameRate = config.TargetFps;
            Debug.Log($"[GameBootstrap] Boot scene initialized. Config valid: {config.Validate()}");
        }
    }
}
