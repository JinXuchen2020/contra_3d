using UnityEngine;
using Contra3D.Core;

namespace Contra3D.Runtime
{
    /// <summary>相机跟随 MonoBehaviour，阻尼 ≤0.15s。</summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        private Vector3 _targetPosition;
        private float _dampingSec = 0.15f;

        public void Apply(CameraParams p)
        {
            _targetPosition = p.Position;
        }

        private void FixedUpdate()
        {
            float t = Time.fixedDeltaTime / Mathf.Max(_dampingSec, 0.001f);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
            transform.LookAt(_targetPosition + transform.forward);
        }
    }
}
