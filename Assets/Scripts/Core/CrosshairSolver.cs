using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>准星世界→屏幕坐标纯函数求解。</summary>
    public static class CrosshairSolver
    {
        /// <summary>将世界坐标投影到屏幕坐标。</summary>
        public static Vector2 WorldToScreenPoint(Vector3 worldPos, CameraParams cam)
        {
            // 简化投影：从相机位置向 worldPos 方向发射射线，计算与屏幕平面的交点
            Vector3 toTarget = worldPos - cam.Position;
            float distance = Vector3.Dot(toTarget, GetForward(cam));
            if (distance <= 0) return new Vector2(0.5f, 0.5f); // 在相机后方

            float fovRad = cam.FovDeg * ((float)System.Math.PI / 180f);
            float tanHalfFov = (float)System.Math.Tan(fovRad / 2f);
            float screenX = toTarget.X / (distance * tanHalfFov);
            float screenY = toTarget.Y / (distance * tanHalfFov);

            // 归一化到 [0,1] 屏幕空间（中心为 0.5,0.5）
            return new Vector2(0.5f + screenX * 0.5f, 0.5f - screenY * 0.5f);
        }

        /// <summary>根据瞄准方向求解准星屏幕位置。</summary>
        public static Vector2 GetCrosshairScreenPos(Vector3 aimDir, CameraParams cam)
        {
            // aimDir 是相机前方的单位向量，准星始终在屏幕中心
            return new Vector2(0.5f, 0.5f);
        }

        private static Vector3 GetForward(CameraParams cam)
        {
            float cy = (float)System.Math.Cos(cam.Yaw);
            float sy = (float)System.Math.Sin(cam.Yaw);
            float cp = (float)System.Math.Cos(cam.Pitch);
            float sp = (float)System.Math.Sin(cam.Pitch);
            return new Vector3(cy * cp, sp, -sy * cp);
        }
    }
}
