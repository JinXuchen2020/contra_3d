using UnityEngine;
using Contra3D.Core;

namespace Contra3D.Runtime
{
    /// <summary>
    /// T-SYS-002 — CharacterController 驱动（player_movement.md 分层设计/数据流）。
    /// FixedUpdate: 组装 MotorInput（isGrounded 回填）→ PlayerMotor.Simulate → Move。
    /// 只绑定与转发，不含游戏规则；选型依据 unity_physics.md（角色非刚体动力学）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInputAdapter _input;

        private CharacterController _characterController;
        private Camera _pitchCamera;
        private MotorState _state;
        private MotorConfig _config;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _config = MotorConfig.Default();
            _state = MotorState.Initial(transform.eulerAngles.y * Mathf.Deg2Rad);
            if (_pitchCamera == null)
            {
                _pitchCamera = GetComponentInChildren<Camera>();
            }
        }

        private void FixedUpdate()
        {
            if (_input == null)
            {
                return;
            }

            MotorInput input = new MotorInput
            {
                MoveXZ = _input.Move,
                LookDelta = _input.Look,
                JumpPressed = _input.ConsumeJumpPressed(),
                JumpHeld = _input.JumpHeld,
                IsGrounded = _characterController.isGrounded
            };

            float dt = Time.fixedDeltaTime;
            _state = PlayerMotor.Simulate(_state, input, _config, dt);

            // 位移交由 CharacterController 碰撞解算；解算后的实际位置回填 state（外部 y 修正通道）
            Vector3 displacement = new Vector3(_state.Velocity.X, _state.Velocity.Y, _state.Velocity.Z) * dt;
            _characterController.Move(displacement);
            Vector3 actual = transform.position;
            _state.Position = new Vector3(actual.x, actual.y, actual.z);

            // 朝向：yaw 驱动本体，pitch 驱动子相机（弧度 → 欧拉角）
            transform.rotation = Quaternion.Euler(0f, _state.Yaw * Mathf.Rad2Deg, 0f);
            if (_pitchCamera != null)
            {
                _pitchCamera.transform.localRotation = Quaternion.Euler(_state.Pitch * Mathf.Rad2Deg, 0f, 0f);
            }
        }
    }
}
