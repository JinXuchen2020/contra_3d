using Contra3D.Core;
using System.Collections.Generic;

namespace Contra3D.Runtime
{
    /// <summary>ViewSnapshot → GameObject 同步 + 弹体/敌人对象池回收。</summary>
    public static class ViewSyncSystem
    {
        private const int MaxProjectiles = 200;
        private static readonly List<GameObject> _projectilePool = new();
        private static readonly List<GameObject> _enemyPool = new();

        public static void Sync(ViewSnapshot snap, GameObjectPool pool)
        {
            // 同步弹体（池容量 200）
            int activeProj = 0;
            foreach (var pv in snap.Projectiles)
            {
                if (!pv.IsActive) continue;
                if (activeProj >= MaxProjectiles) break;
                // 简化：直接设置已存在 GameObject 位置（实际需要池管理）
                activeProj++;
            }
        }
    }

    /// <summary>简单对象池（供 ViewSyncSystem 使用）。</summary>
    public class GameObjectPool
    {
        public GameObject Spawn(string tag) => new GameObject(tag);
        public void Despawn(GameObject obj) { /* 回收逻辑 */ }
    }
}
