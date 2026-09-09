using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>相机纯数据参数，供 Core 求解与 Runtime 同步。</summary>
    public readonly struct CameraParams
    {
        public Vector3 Position { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public float FovDeg { get; }

        public CameraParams(Vector3 position, float yaw, float pitch, float fovDeg = 60.0f)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            FovDeg = fovDeg;
        }
    }
}
