using System;
using System.Collections.Generic;
using AS.Core;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>
    /// A pooled bullet. Moves at constant velocity, damages the first <see cref="IDamageable"/> it
    /// touches and despawns on any hit or when its lifetime runs out. What it can touch is decided
    /// by its physics layer (PlayerShot / EnemyShot). Announces each hit as a <see cref="ProjectileImpact"/>.
    /// Each arena keeps a list of its bullets in flight, for the agent to observe (<see cref="GetLive"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        Rigidbody2D _body;
        EventChannel _events;
        Action<Projectile> _release;
        DamageInfo _damage;
        float _despawnTime;
        bool _live;
        Arena _arena;

        static readonly Dictionary<Arena, HashSet<Projectile>> Live = new();
        static int _enemyShotLayer = -1;

        // Domain reload is off for Play mode, so the lists would otherwise carry over between editor sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewRun() => Live.Clear();

        public Vector2 Velocity => _body.linearVelocity;

        /// <summary>Whether an enemy fired it, so it can hurt the player.</summary>
        public bool IsEnemyShot
        {
            get
            {
                if (_enemyShotLayer < 0) _enemyShotLayer = LayerMask.NameToLayer(Layers.EnemyShot);
                return gameObject.layer == _enemyShotLayer;
            }
        }

        /// <summary>Fills <paramref name="results"/> with the bullets in flight in <paramref name="arena"/>.</summary>
        public static void GetLive(Arena arena, List<Projectile> results)
        {
            results.Clear();
            if (arena != null && Live.TryGetValue(arena, out var set)) results.AddRange(set);
        }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _events = Arena.EventsFor(this);
            _arena = Arena.Of(this);
        }

        // Pools release bullets on an arena reset without a Despawn, so leaving the list goes here.
        void OnDisable()
        {
            if (_arena != null && Live.TryGetValue(_arena, out var set)) set.Remove(this);
        }

        public void Launch(Vector2 direction, float speed, float lifetime, in DamageInfo damage, Action<Projectile> release)
        {
            _body.position = transform.position;
            _body.rotation = transform.eulerAngles.z;
            _body.linearVelocity = direction * speed;
            _damage = damage;
            _release = release;
            _despawnTime = Time.time + lifetime;
            _live = true;
            if (_arena != null)
            {
                if (!Live.TryGetValue(_arena, out var set)) Live[_arena] = set = new HashSet<Projectile>();
                set.Add(this);
            }
        }

        void Update()
        {
            if (_live && Time.time >= _despawnTime) Despawn();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_live) return;
            other.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            _events.Publish(new ProjectileImpact(transform.position));
            Despawn();
        }

        /// <summary>Removes the projectile without hitting anything (a bomb clearing the air).</summary>
        public void Despawn()
        {
            if (!_live) return;
            _live = false;
            _body.linearVelocity = Vector2.zero;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
