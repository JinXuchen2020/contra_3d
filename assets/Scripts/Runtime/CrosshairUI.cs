// T-SYS-001 (rendering) — 准星 UI 脚本。
// 设计来源: _os_state/design_contracts/rendering.yaml runtime_types.CrosshairUI
// 职责: Screen Space Canvas 十字线；仅驱动 RectTransform，屏幕坐标由 Core 求解后经 SetScreenPos 下发。
// 边界: 本脚本不含游戏规则（世界→屏幕求解逻辑属 Core 的 CrosshairSolver，后续任务并入）。

using UnityEngine;

namespace Contra3D.Runtime
{
    /// <summary>
    /// 准星 UI — 十字线 RectTransform 的屏幕坐标驱动器。
    /// </summary>
    public sealed class CrosshairUI : MonoBehaviour
    {
        [SerializeField]
        private RectTransform crosshairRoot;

        /// <summary>
        /// 设置准星屏幕坐标（对齐契约签名 SetScreenPos(Vector2 screenPos)）。
        /// </summary>
        public void SetScreenPos(Vector2 screenPos)
        {
            if (crosshairRoot == null)
            {
                Debug.LogError("[CrosshairUI] crosshairRoot is not assigned.");
                return;
            }

            crosshairRoot.position = screenPos;
        }

        private void Awake()
        {
            if (crosshairRoot == null)
            {
                // 容错：未在 Inspector 指定时尝试取自身 RectTransform。
                crosshairRoot = GetComponent<RectTransform>();
            }

            // TODO(PlayMode): 依赖 Unity Editor — 准星随相机朝向对应屏幕中心的验证
            // （rendering.yaml acceptance: "准星 UI 显示且随相机朝向正确对应屏幕中心/世界目标点"）；
            // Editor 未安装期留待 PlayMode 冒烟。
        }
    }
}
