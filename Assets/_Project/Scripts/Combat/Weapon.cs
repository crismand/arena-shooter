using AS.Core;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>Fires pooled projectiles from a muzzle, limited by the weapon's fire rate.</summary>
    public class Weapon : MonoBehaviour
    {
        [SerializeField] WeaponData data;
        [SerializeField] Transform muzzle;

        PrefabPool<Projectile> _pool;
        float _nextShotTime;

        public WeaponData Data => data;

        void Awake()
        {
            _pool = new PrefabPool<Projectile>(data.ProjectilePrefab);
            if (muzzle == null) muzzle = transform;
        }

        /// <summary>Fires toward <paramref name="direction"/> if the weapon is off cooldown.</summary>
        public bool TryFire(Vector2 direction)
        {
            if (Time.time < _nextShotTime || direction.sqrMagnitude < 0.0001f) return false;
            _nextShotTime = Time.time + 1f / data.FireRate;

            direction.Normalize();
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = data.ProjectileCount;

            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : Mathf.Lerp(-data.SpreadAngle, data.SpreadAngle, i / (count - 1f)) * 0.5f;
                var rotation = Quaternion.Euler(0f, 0f, baseAngle + offset);
                Vector2 shotDirection = rotation * Vector3.right;

                var projectile = _pool.Get(muzzle.position, rotation);
                projectile.Launch(shotDirection, data.ProjectileSpeed, data.ProjectileLifetime,
                    new DamageInfo(data.Damage, shotDirection, gameObject), _pool.Release);
            }
            return true;
        }
    }
}
