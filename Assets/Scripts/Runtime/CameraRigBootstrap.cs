// T-SYS-001 (rendering) — 相机装置引导脚本。
// 设计来源: templates/system_design/rendering_system.md（主相机 Perspective, 60° FOV）
// 职责: 确保 Boot 场景主相机满足设计数值约束；后续相机跟随（阻尼 ≤0.15s）由 Core 状态驱动。
// 依赖: UnityEngine（Contra3D.Runtime 程序集）。

using UnityEngine;
using Contra3D.Core;

namespace Contra3D.Runtime
{
    /// <summary>
    /// 相机装置引导 — 校准主相机 FOV 等渲染约束参数。
    /// </summary>
    public sealed class CameraRigBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[CameraRigBootstrap] MainCamera not found in Boot scene.");
                return;
            }

            var config = RenderConfig.Default;
            mainCamera.fieldOfView = config.DefaultFovDeg;
            Debug.Log($"[CameraRigBootstrap] Camera rig calibrated. FOV={config.DefaultFovDeg} deg.");
        }
    }
}
