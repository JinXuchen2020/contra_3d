using UnityEngine;
using UnityEngine.InputSystem;

namespace Contra3D.Runtime
{
    /// <summary>
    /// T-SYS-002 — Input System 桥接（player_movement.md 分层设计）。
    /// 轮询 Move/Look，Jump performed 打边沿标记供 FixedUpdate 消费。
    /// 只绑定与转发，不含游戏规则（禁止回边不变量）。
    /// 键位运行时构造（不依赖 InputActions 生成代码），无硬编码规则逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputAdapter : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private bool _jumpEdgePending;

        /// <summary>平面移动轴（x=右, y=前），键盘 WASD + 手柄左摇杆。</summary>
        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>鼠标/手柄视角增量（count/tick）。</summary>
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>跳跃键当前按住（可变跳跃截断用）。</summary>
        public bool JumpHeld => _jump != null && _jump.IsPressed();

        /// <summary>消费一次跳跃按下沿（FixedUpdate 中调用，消费后归零）。</summary>
        public bool ConsumeJumpPressed()
        {
            bool edge = _jumpEdgePending;
            _jumpEdgePending = false;
            return edge;
        }

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");

            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _jump.performed += OnJumpPerformed;
        }

        private void OnEnable()
        {
            _move.Enable();
            _look.Enable();
            _jump.Enable();
        }

        private void OnDisable()
        {
            _move.Disable();
            _look.Disable();
            _jump.Disable();
        }

        private void OnDestroy()
        {
            _jump.performed -= OnJumpPerformed;
            _move.Dispose();
            _look.Dispose();
            _jump.Dispose();
        }

        private void OnJumpPerformed(InputAction.CallbackContext _)
        {
            _jumpEdgePending = true;
        }
    }
}
