using System.Collections.Generic;
using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// Listens for <see cref="EnemyKilled"/> in its arena and, using the enemy's drop chance, leaves a
    /// power-up where it died, picked by each power-up's drop weight. Pickups are pooled.
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
            var powerUp = PickWeighted(powerUps, Random.value);
            if (powerUp != null) Spawn(powerUp, evt.Position);
        }

        /// <summary>
        /// The power-up <paramref name="roll"/> (0 to 1) lands on when each takes a share of the range
        /// in proportion to its drop weight. Null if every weight is zero.
        /// </summary>
        public static PowerUp PickWeighted(IReadOnlyList<PowerUp> choices, float roll)
        {
            float total = 0f;
            foreach (var choice in choices) total += choice.DropWeight;
            if (total <= 0f) return null;

            float point = Mathf.Clamp01(roll) * total;
            PowerUp last = null;
            foreach (var choice in choices)
            {
                if (choice.DropWeight <= 0f) continue;
                last = choice;
                if (point < choice.DropWeight) return choice;
                point -= choice.DropWeight;
            }
            // A roll of exactly 1 lands on the end.
            return last;
        }

        public Pickup Spawn(PowerUp powerUp, Vector2 position)
        {
            var pickup = _pool.Get(position, Quaternion.identity);
            pickup.Init(powerUp, pickupLifetime, _pool.Release);
            return pickup;
        }
    }
}
