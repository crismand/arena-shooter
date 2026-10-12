using System;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>
    /// A pooled bullet. Moves at constant velocity, damages the first <see cref="IDamageable"/> it
    /// touches and despawns on any hit or when its lifetime runs out. What it can touch is decided
    /// by its physics layer (PlayerShot / EnemyShot).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        Rigidbody2D _body;
        Action<Projectile> _release;
        DamageInfo _damage;
        float _despawnTime;
        bool _live;

        void Awake() => _body = GetComponent<Rigidbody2D>();

        public void Launch(Vector2 direction, float speed, float lifetime, in DamageInfo damage, Action<Projectile> release)
        {
            _body.position = transform.position;
            _body.rotation = transform.eulerAngles.z;
            _body.linearVelocity = direction * speed;
            _damage = damage;
            _release = release;
            _despawnTime = Time.time + lifetime;
            _live = true;
        }

        void Update()
        {
            if (_live && Time.time >= _despawnTime) Despawn();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_live) return;
            other.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            Despawn();
        }

        void Despawn()
        {
            if (!_live) return;
            _live = false;
            _body.linearVelocity = Vector2.zero;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
