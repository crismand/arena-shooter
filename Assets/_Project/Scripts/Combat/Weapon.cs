using System.Collections.Generic;
using AS.Core;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>
    /// Fires pooled projectiles from a muzzle, limited by the weapon's fire rate. Power-ups can
    /// temporarily swap in different <see cref="WeaponData"/> with <see cref="SetOverride"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [SerializeField] WeaponData data;
        [SerializeField] Transform muzzle;

        // One pool per projectile prefab, so an override with a different bullet still pools.
        readonly Dictionary<Projectile, PrefabPool<Projectile>> _pools = new();
        WeaponData _override;
        float _nextShotTime;
        // Bullets live under the shooter's arena so they publish on its events, not another arena's.
        Transform _poolParent;

        /// <summary>The data currently in use: the override if one is set, otherwise the base weapon.</summary>
        public WeaponData Data => _override != null ? _override : data;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            var arena = Arena.Of(this);
            if (arena != null) _poolParent = arena.transform;
        }

        /// <summary>Use <paramref name="weaponData"/> instead of the base weapon until cleared with null.</summary>
        public void SetOverride(WeaponData weaponData) => _override = weaponData;

        /// <summary>Fires toward <paramref name="direction"/> if the weapon is off cooldown.</summary>
        public bool TryFire(Vector2 direction)
        {
            if (Time.time < _nextShotTime || direction.sqrMagnitude < 0.0001f) return false;
            var current = Data;
            _nextShotTime = Time.time + 1f / current.FireRate;

            if (!_pools.TryGetValue(current.ProjectilePrefab, out var pool))
                _pools[current.ProjectilePrefab] = pool = new PrefabPool<Projectile>(current.ProjectilePrefab, _poolParent);

            direction.Normalize();
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = current.ProjectileCount;

            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : Mathf.Lerp(-current.SpreadAngle, current.SpreadAngle, i / (count - 1f)) * 0.5f;
                var rotation = Quaternion.Euler(0f, 0f, baseAngle + offset);
                Vector2 shotDirection = rotation * Vector3.right;

                var projectile = pool.Get(muzzle.position, rotation);
                projectile.Launch(shotDirection, current.ProjectileSpeed, current.ProjectileLifetime,
                    new DamageInfo(current.Damage, shotDirection, gameObject), pool.Release);
            }
            return true;
        }
    }
}
