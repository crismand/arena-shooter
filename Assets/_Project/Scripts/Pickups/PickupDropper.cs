using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// Listens for <see cref="EnemyKilled"/> in its arena and, using the enemy's drop chance, leaves a
    /// random power-up where it died. Pickups are pooled.
    /// </summary>
    public class PickupDropper : MonoBehaviour
    {
        [SerializeField] Pickup pickupPrefab;
        [SerializeField] PowerUp[] powerUps;
        [Tooltip("Seconds a dropped pickup stays before vanishing.")]
        [SerializeField, Min(1f)] float pickupLifetime = 10f;

        PrefabPool<Pickup> _pool;
        EventChannel _events;

        public int ActiveCount => _pool.CountActive;

        void Awake()
        {
            _pool = new PrefabPool<Pickup>(pickupPrefab, transform);
            _events = Arena.EventsFor(this);
            _events.Subscribe<ArenaReset>(OnArenaReset);
        }

        void OnDestroy() => _events.Unsubscribe<ArenaReset>(OnArenaReset);

        void OnArenaReset(ArenaReset _) => _pool.ReleaseAll();

        void OnEnable() => _events.Subscribe<EnemyKilled>(OnEnemyKilled);
        void OnDisable() => _events.Unsubscribe<EnemyKilled>(OnEnemyKilled);

        void OnEnemyKilled(EnemyKilled evt)
        {
            if (powerUps.Length == 0 || Random.value >= evt.DropChance) return;
            Spawn(powerUps[Random.Range(0, powerUps.Length)], evt.Position);
        }

        public Pickup Spawn(PowerUp powerUp, Vector2 position)
        {
            var pickup = _pool.Get(position, Quaternion.identity);
            pickup.Init(powerUp, pickupLifetime, _pool.Release);
            return pickup;
        }
    }
}
