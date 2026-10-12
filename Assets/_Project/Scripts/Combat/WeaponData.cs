using UnityEngine;

namespace AS.Combat
{
    [CreateAssetMenu(menuName = "AS/Weapon Data", fileName = "Weapon")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] Projectile projectilePrefab;
        [Tooltip("Shots per second while the trigger is held.")]
        [SerializeField, Min(0.1f)] float fireRate = 8f;
        [SerializeField, Min(0f)] float projectileSpeed = 18f;
        [Tooltip("Seconds before an unobstructed projectile despawns.")]
        [SerializeField, Min(0.05f)] float projectileLifetime = 1.5f;
        [SerializeField, Min(0f)] float damage = 1f;
        [Tooltip("Projectiles per shot, fanned evenly across Spread Angle.")]
        [SerializeField, Min(1)] int projectileCount = 1;
        [SerializeField, Range(0f, 180f)] float spreadAngle;

        public Projectile ProjectilePrefab => projectilePrefab;
        public float FireRate => fireRate;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float Damage => damage;
        public int ProjectileCount => projectileCount;
        public float SpreadAngle => spreadAngle;
    }
}
