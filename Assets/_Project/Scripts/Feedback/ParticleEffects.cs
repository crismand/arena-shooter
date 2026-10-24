using System.Collections.Generic;
using AS.Core;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// Plays pooled particle bursts where things happen: enemy deaths (in the enemy's colour),
    /// bullet impacts, player hits and death, and pickups. Any slot left empty is simply skipped.
    /// </summary>
    public class ParticleEffects : MonoBehaviour
    {
        [SerializeField] PooledEffect enemyDeath;
        [SerializeField] PooledEffect impact;
        [SerializeField] PooledEffect playerHit;
        [SerializeField] PooledEffect playerDeath;
        [SerializeField] PooledEffect pickupCollected;

        readonly Dictionary<PooledEffect, PrefabPool<PooledEffect>> _pools = new();

        void OnEnable()
        {
            EventBus.Subscribe<EnemyKilled>(OnEnemyKilled);
            EventBus.Subscribe<ProjectileImpact>(OnProjectileImpact);
            EventBus.Subscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
            EventBus.Subscribe<PickupCollected>(OnPickupCollected);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
            EventBus.Unsubscribe<ProjectileImpact>(OnProjectileImpact);
            EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
            EventBus.Unsubscribe<PickupCollected>(OnPickupCollected);
        }

        void OnEnemyKilled(EnemyKilled evt) => Play(enemyDeath, evt.Position, evt.Color);
        void OnProjectileImpact(ProjectileImpact evt) => Play(impact, evt.Position);
        void OnPlayerDamaged(PlayerDamaged evt) => Play(playerHit, evt.Position);
        void OnPlayerDied(PlayerDied evt) => Play(playerDeath, evt.Position);
        void OnPickupCollected(PickupCollected evt) => Play(pickupCollected, evt.Position, evt.Color);

        /// <summary>Plays <paramref name="prefab"/> at <paramref name="position"/>. The default <paramref name="tint"/> keeps its own colour.</summary>
        public void Play(PooledEffect prefab, Vector2 position, Color tint = default)
        {
            if (prefab == null) return;
            if (!_pools.TryGetValue(prefab, out var pool))
                _pools[prefab] = pool = new PrefabPool<PooledEffect>(prefab, transform);
            pool.Get(position, Quaternion.identity).Play(tint, pool.Release);
        }

        /// <summary>Bursts currently playing, across all pools.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var pool in _pools.Values) count += pool.CountActive;
                return count;
            }
        }
    }
}
