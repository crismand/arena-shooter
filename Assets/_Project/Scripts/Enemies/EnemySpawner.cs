using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Spawns pooled enemies at a fixed interval from spawn points that aren't too close to the
    /// target. Step 3's wave director will take over the timing; <see cref="Spawn"/> stays the entry point.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] EnemyBase prefab;
        [SerializeField] Transform target;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField, Min(0.05f)] float interval = 1.5f;
        [SerializeField, Min(0f)] float initialDelay = 1f;
        [SerializeField, Min(1)] int maxAlive = 12;
        [Tooltip("Spawn points closer than this to the target are skipped.")]
        [SerializeField, Min(0f)] float minDistanceFromTarget = 6f;

        PrefabPool<EnemyBase> _pool;
        float _nextSpawnTime;

        public int ActiveCount => _pool.CountActive;

        void Awake() => _pool = new PrefabPool<EnemyBase>(prefab);

        void OnEnable() => _nextSpawnTime = Time.time + initialDelay;

        void Update()
        {
            if (Time.time < _nextSpawnTime) return;
            _nextSpawnTime = Time.time + interval;

            if (ActiveCount >= maxAlive || target == null || !target.gameObject.activeInHierarchy) return;
            if (TryPickSpawnPoint(out var position)) Spawn(position);
        }

        public EnemyBase Spawn(Vector2 position)
        {
            var enemy = _pool.Get(position, Quaternion.identity);
            enemy.Init(target, _pool.Release);
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
