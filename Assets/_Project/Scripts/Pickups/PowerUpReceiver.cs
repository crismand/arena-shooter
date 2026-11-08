using System.Collections.Generic;
using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// Sits on the player and carries out power-up effects: timed ones (a weapon override and a
    /// shield) and instant ones (healing and the bomb). Picking up the same timed kind again restarts
    /// its timer; a new weapon boost replaces the old one.
    /// </summary>
    public class PowerUpReceiver : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] Health health;
        [Tooltip("Shown while the shield is up.")]
        [SerializeField] GameObject shieldVisual;
        [Tooltip("Layers a bomb damages.")]
        [SerializeField] LayerMask bombTargets;
        [Tooltip("Layers of projectiles a bomb clears.")]
        [SerializeField] LayerMask bombClears;

        float _weaponBoostEndsAt;
        float _shieldEndsAt;
        readonly HashSet<IDamageable> _bombed = new();

        public bool HasWeaponBoost => _weaponBoostEndsAt > 0f;
        public bool HasShield => _shieldEndsAt > 0f;

        EventChannel _events;

        void Awake()
        {
            _events = Arena.EventsFor(this);
            _events.Subscribe<ArenaReset>(OnArenaReset);
            if (shieldVisual != null) shieldVisual.SetActive(false);
        }

        void OnDestroy() => _events.Unsubscribe<ArenaReset>(OnArenaReset);

        void OnArenaReset(ArenaReset _)
        {
            weapon.SetOverride(null);
            _weaponBoostEndsAt = 0f;
            _shieldEndsAt = 0f;
            if (shieldVisual != null) shieldVisual.SetActive(false);
        }

        public void BoostWeapon(WeaponData boosted, float duration, string label)
        {
            weapon.SetOverride(boosted);
            _weaponBoostEndsAt = Time.time + duration;
            _events.Publish(new PowerUpActivated(label, duration));
        }

        public void RaiseShield(float duration, string label)
        {
            health.GrantInvulnerability(duration);
            _shieldEndsAt = Time.time + duration;
            if (shieldVisual != null) shieldVisual.SetActive(true);
            _events.Publish(new PowerUpActivated(label, duration));
        }

        /// <summary>Restores up to <paramref name="amount"/> health. False if it was already full.</summary>
        public bool Heal(float amount) => health.Heal(amount);

        /// <summary>
        /// Clears enemy projectiles and damages every target within <paramref name="radius"/> in this
        /// arena (the radius is meant to cover the whole arena). Returns how many targets were hit.
        /// </summary>
        public int DetonateBomb(float damage, float radius)
        {
            Vector2 origin = transform.position;
            var arena = Arena.Of(this);

            // Other arenas can share the space (training), so only touch things in this one.
            foreach (var hit in Physics2D.OverlapCircleAll(origin, radius, bombClears))
            {
                var projectile = hit.GetComponentInParent<Projectile>();
                if (projectile != null && Arena.Of(projectile) == arena) projectile.Despawn();
            }

            int count = 0;
            _bombed.Clear();
            foreach (var hit in Physics2D.OverlapCircleAll(origin, radius, bombTargets))
            {
                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || Arena.Of(hit) != arena || !_bombed.Add(target)) continue;
                Vector2 direction = ((Vector2)hit.transform.position - origin).normalized;
                target.TakeDamage(new DamageInfo(damage, direction, gameObject));
                count++;
            }

            _events.Publish(new BombDetonated(origin, radius));
            return count;
        }

        void Update()
        {
            if (HasWeaponBoost && Time.time >= _weaponBoostEndsAt)
            {
                weapon.SetOverride(null);
                _weaponBoostEndsAt = 0f;
            }

            if (HasShield && Time.time >= _shieldEndsAt)
            {
                if (shieldVisual != null) shieldVisual.SetActive(false);
                _shieldEndsAt = 0f;
            }
        }
    }
}
