using System;
using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Shared enemy behaviour: steering toward a desired velocity, facing its movement, damaging
    /// whatever it touches on <see cref="contactTargets"/>, and announcing its death
    /// (<see cref="EnemyKilled"/>) before returning to its pool.
    /// Subclasses only decide where they want to go.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [SerializeField] protected EnemyData data;
        [Tooltip("Rotated to face the direction of travel.")]
        [SerializeField] Transform visual;
        [Tooltip("Layers this enemy damages by touching.")]
        [SerializeField] LayerMask contactTargets;

        Action<EnemyBase> _release;

        public EnemyData Data => data;
        public Health Health { get; private set; }
        protected Rigidbody2D Body { get; private set; }
        protected Transform Target { get; private set; }
        protected bool HasTarget => Target != null && Target.gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
        }

        void OnEnable() => Health.Died += OnDied;
        void OnDisable() => Health.Died -= OnDied;

        /// <summary>Called by the spawner each time this enemy is taken from the pool.</summary>
        public void Init(Transform target, Action<EnemyBase> release)
        {
            Target = target;
            _release = release;
            Health.ResetHealth(data.MaxHealth);
            Body.linearVelocity = Vector2.zero;
            OnSpawned();
        }

        /// <summary>Velocity the enemy steers toward this physics step. Only called while it has a target.</summary>
        protected abstract Vector2 DesiredVelocity();

        /// <summary>Hook for per-spawn setup in subclasses (timers, random choices). Runs at the end of <see cref="Init"/>.</summary>
        protected virtual void OnSpawned() { }

        /// <summary>Direction the visual should face. Defaults to the direction of travel.</summary>
        protected virtual Vector2 FacingDirection(Vector2 desiredVelocity) => desiredVelocity;

        protected virtual void FixedUpdate()
        {
            Vector2 desired = HasTarget ? DesiredVelocity() : Vector2.zero;
            Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, desired, data.Acceleration * Time.fixedDeltaTime);

            Vector2 facing = FacingDirection(desired);
            if (visual != null && facing.sqrMagnitude > 0.01f)
                visual.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if ((contactTargets.value & (1 << collision.gameObject.layer)) == 0) return;
            Vector2 direction = ((Vector2)(collision.transform.position - transform.position)).normalized;
            collision.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo(data.ContactDamage, direction, gameObject));
        }

        void OnDied()
        {
            EventBus.Publish(new EnemyKilled(data.ScoreValue, transform.position, data.DropChance));
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
