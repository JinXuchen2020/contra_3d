using UnityEngine;

namespace Contra3D.Runtime
{
    /// <summary>
    /// T-SYS-002 — Input System 桥接（player_movement.md 分层设计）。
    /// 使用 Unity 传统输入系统（兼容所有 Unity 版本，无需 Input System 包）。
    /// 轮询 Move/Look，Jump performed 打边沿标记供 FixedUpdate 消费。
    /// 键位硬编码（WASD + Space），无运行时配置依赖。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputAdapter : MonoBehaviour
    {
        private bool _jumpEdgePending;

        /// <summary>平面移动轴（x=右, y=前），键盘 WASD。</summary>
        public Vector2 Move
        {
            get
            {
                float h = Input.GetAxis("Horizontal");
                float v = Input.GetAxis("Vertical");
                return new Vector2(h, v);
            }
        }

        /// <summary>鼠标视角增量（count/tick）。</summary>
        public Vector2 Look
        {
            get
            {
                // 鼠标 delta 通过 Input.GetAxis 获取（Unity 默认映射）
                float hx = Input.GetAxis("Mouse X");
                float hy = Input.GetAxis("Mouse Y");
                return new Vector2(hx, hy);
            }
        }

        /// <summary>跳跃键当前按住。</summary>
        public bool JumpHeld => Input.GetKey(KeyCode.Space);

        /// <summary>消费一次跳跃按下沿（FixedUpdate 中调用，消费后归零）。</summary>
        public bool ConsumeJumpPressed()
        {
            bool edge = _jumpEdgePending;
            _jumpEdgePending = false;
            return edge;
        }

        private void Update()
        {
            // 检测跳跃按下沿
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _jumpEdgePending = true;
            }
        }
    }
}
