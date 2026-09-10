using Contra3D.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Contra3D.Runtime
{
    /// <summary>ViewSnapshot → GameObject 同步 + 弹体/敌人对象池回收。</summary>
    public static class ViewSyncSystem
    {
        private const int MaxProjectiles = 200;

        // Projectile state: id → active GameObject
        private static readonly Dictionary<string, GameObject> _activeProjectiles = new();
        private static readonly List<GameObject> _projectilePool = new();

        // Enemy state: id → active GameObject
        private static readonly Dictionary<string, GameObject> _activeEnemies = new();
        private static readonly List<GameObject> _enemyPool = new();

        /// <summary>
        /// 将 ViewSnapshot 的弹体和敌人数据同步到场景中的 GameObject。
        /// 弹体超过 MaxProjectiles 时丢弃最旧条目；未激活的条目返回池中。
        /// </summary>
        public static void Sync(ViewSnapshot snap, GameObjectPool pool)
        {
            SyncProjectiles(snap.Projectiles, pool);
            SyncEnemies(snap.Enemies, pool);
        }

        private static void SyncProjectiles(IReadOnlyList<ProjectileViewData> projectiles, GameObjectPool pool)
        {
            int activeCount = 0;

            // 1. 同步/创建活跃弹体
            foreach (var pv in projectiles)
            {
                if (!pv.IsActive) continue;

                if (_activeProjectiles.TryGetValue(pv.Id, out var existing))
                {
                    // 更新已有弹体位置
                    existing.transform.position = ToUnity(pv.Position);
                }
                else if (_projectilePool.Count > 0)
                {
                    // 从池中取出复用
                    var go = _projectilePool[_projectilePool.Count - 1];
                    _projectilePool.RemoveAt(_projectilePool.Count - 1);
                    go.SetActive(true);
                    go.transform.position = ToUnity(pv.Position);
                    _activeProjectiles[pv.Id] = go;
                }
                else if (activeCount < MaxProjectiles)
                {
                    // 池空且未达上限，新建
                    var go = pool.Spawn("Projectile");
                    go.transform.position = ToUnity(pv.Position);
                    _activeProjectiles[pv.Id] = go;
                    activeCount++;
                }
                // else: 超出上限，跳过
            }

            // 2. 回收不再活跃的弹体
            var toRemove = new List<string>();
            foreach (var kvp in _activeProjectiles)
            {
                bool stillActive = false;
                foreach (var pv in projectiles)
                {
                    if (pv.Id == kvp.Key && pv.IsActive)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive)
                    toRemove.Add(kvp.Key);
            }

            foreach (var id in toRemove)
            {
                if (_activeProjectiles.TryGetValue(id, out var go))
                {
                    go.SetActive(false);
                    _projectilePool.Add(go);
                    _activeProjectiles.Remove(id);
                }
            }
        }

        private static void SyncEnemies(IReadOnlyList<EnemyViewData> enemies, GameObjectPool pool)
        {
            // 1. 同步/创建活跃敌人
            foreach (var ev in enemies)
            {
                if (!ev.IsActive) continue;

                if (_activeEnemies.TryGetValue(ev.Id, out var existing))
                {
                    existing.transform.position = ToUnity(ev.Position);
                    existing.transform.rotation = Quaternion.Euler(0f, ev.YawDeg, 0f);
                }
                else if (_enemyPool.Count > 0)
                {
                    var go = _enemyPool[_enemyPool.Count - 1];
                    _enemyPool.RemoveAt(_enemyPool.Count - 1);
                    go.SetActive(true);
                    go.transform.position = ToUnity(ev.Position);
                    go.transform.rotation = Quaternion.Euler(0f, ev.YawDeg, 0f);
                    _activeEnemies[ev.Id] = go;
                }
                else
                {
                    var go = pool.Spawn("Enemy");
                    go.transform.position = ToUnity(ev.Position);
                    go.transform.rotation = Quaternion.Euler(0f, ev.YawDeg, 0f);
                    _activeEnemies[ev.Id] = go;
                }
            }

            // 2. 回收不再活跃的敌人
            var toRemove = new List<string>();
            foreach (var kvp in _activeEnemies)
            {
                bool stillActive = false;
                foreach (var ev in enemies)
                {
                    if (ev.Id == kvp.Key && ev.IsActive)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive)
                    toRemove.Add(kvp.Key);
            }

            foreach (var id in toRemove)
            {
                if (_activeEnemies.TryGetValue(id, out var go))
                {
                    go.SetActive(false);
                    _enemyPool.Add(go);
                    _activeEnemies.Remove(id);
                }
            }
        }

        /// <summary>将指定 GameObject 归还到弹体池中。</summary>
        public static void DespawnProjectile(GameObject obj)
        {
            obj.SetActive(false);
            _projectilePool.Add(obj);
        }

        /// <summary>将指定 GameObject 归还到敌人池中。</summary>
        public static void DespawnEnemy(GameObject obj)
        {
            obj.SetActive(false);
            _enemyPool.Add(obj);
        }

        private static Vector3 ToUnity(Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>简单对象池（供 ViewSyncSystem 使用）。</summary>
    public class GameObjectPool
    {
        private readonly List<GameObject> _available = new();

        /// <summary>从池中取出或新建一个带指定 Tag 的 GameObject。</summary>
        public GameObject Spawn(string tag)
        {
            if (_available.Count > 0)
            {
                var obj = _available[_available.Count - 1];
                _available.RemoveAt(_available.Count - 1);
                obj.name = tag;
                return obj;
            }
            return new GameObject(tag);
        }

        /// <summary>将 GameObject 回收至池中（调用方需先 SetActive(false)）。</summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            _available.Add(obj);
        }
    }
}
