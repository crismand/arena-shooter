using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Pickups
{
    /// <summary>
    /// Sits on the player and carries out power-up effects with timers: a weapon override and a
    /// shield. Picking up the same kind again restarts its timer; a new weapon boost replaces the old one.
    /// </summary>
    public class PowerUpReceiver : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] Health health;
        [Tooltip("Shown while the shield is up.")]
        [SerializeField] GameObject shieldVisual;

        float _weaponBoostEndsAt;
        float _shieldEndsAt;

        public bool HasWeaponBoost => _weaponBoostEndsAt > 0f;
        public bool HasShield => _shieldEndsAt > 0f;

        void Awake()
        {
            if (shieldVisual != null) shieldVisual.SetActive(false);
        }

        public void BoostWeapon(WeaponData boosted, float duration, string label)
        {
            weapon.SetOverride(boosted);
            _weaponBoostEndsAt = Time.time + duration;
            EventBus.Publish(new PowerUpActivated(label, duration));
        }

        public void RaiseShield(float duration, string label)
        {
            health.GrantInvulnerability(duration);
            _shieldEndsAt = Time.time + duration;
            if (shieldVisual != null) shieldVisual.SetActive(true);
            EventBus.Publish(new PowerUpActivated(label, duration));
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
