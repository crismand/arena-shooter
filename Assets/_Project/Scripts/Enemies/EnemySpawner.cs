using System.Collections.Generic;
using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Places pooled enemies (one pool per prefab) at spawn points that aren't too close to the
    /// target. Decides where, never when; the wave director drives the timing.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Transform[] spawnPoints;
        [Tooltip("Spawn points closer than this to the target are skipped.")]
        [SerializeField, Min(0f)] float minDistanceFromTarget = 6f;

        readonly Dictionary<EnemyBase, PrefabPool<EnemyBase>> _pools = new();

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var pool in _pools.Values) count += pool.CountActive;
                return count;
            }
        }

        /// <summary>Spawns at a random safe spawn point. False if the target is gone or every point is too close.</summary>
        public bool TrySpawn(EnemyBase prefab)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            if (!TryPickSpawnPoint(out var position)) return false;
            Spawn(prefab, position);
            return true;
        }

        public EnemyBase Spawn(EnemyBase prefab, Vector2 position)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
                _pools[prefab] = pool = new PrefabPool<EnemyBase>(prefab, transform);

            var enemy = pool.Get(position, Quaternion.identity);
            enemy.Init(target, pool.Release);
            return enemy;
        }

        bool TryPickSpawnPoint(out Vector2 position)
        {
            int start = Random.Range(0, spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                position = spawnPoints[(start + i) % spawnPoints.Length].position;
                if (Vector2.Distance(position, target.position) >= minDistanceFromTarget) return true;
            }
            position = default;
            return false;
        }
    }
}
