// T-SYS-001 (rendering) — 相机装置引导脚本。
// 设计来源: templates/system_design/rendering_system.md（主相机 Perspective, 60° FOV）
// 职责: 确保 Boot 场景主相机满足设计数值约束；后续相机跟随（阻尼 ≤0.15s）由 Core 状态驱动。
// 依赖: UnityEngine（Contra3D.Runtime 程序集）。

using UnityEngine;

namespace Contra3D.Runtime
{
    /// <summary>
    /// 相机装置引导 — 校准主相机 FOV 等渲染约束参数。
    /// </summary>
    public sealed class CameraRigBootstrap : MonoBehaviour
    {
        /// <summary>准星/渲染契约规定的相机 FOV（rendering.yaml crosshair_fov_deg）。</summary>
        private const float FovDeg = 60.0f;

        private void Awake()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[CameraRigBootstrap] MainCamera not found in Boot scene.");
                return;
            }

            mainCamera.fieldOfView = FovDeg;

            // TODO(PlayMode): 依赖 Unity Editor — 相机跟随平滑验证（阻尼 ≤0.15s，
            // 对齐 Core 侧 RenderConfig.CameraDampingSec 默认值）；Editor 未安装期留待 PlayMode 冒烟。
            Debug.Log("[CameraRigBootstrap] Camera rig calibrated.");
        }
    }
}
