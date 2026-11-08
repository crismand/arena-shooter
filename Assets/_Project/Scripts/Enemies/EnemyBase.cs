using System;
using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Shared enemy behaviour: steering toward a desired velocity, facing its movement, damaging
    /// whatever it touches on <see cref="contactTargets"/>, and announcing hits and its death
    /// (<see cref="EnemyDamaged"/>, <see cref="EnemyKilled"/>) before returning to its pool.
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
        // Read once: by the time it dies the damage flash may have tinted the sprite.
        Color _color;

        public EnemyData Data => data;
        public Health Health { get; private set; }
        public Vector2 Velocity => Body.linearVelocity;
        /// <summary>Winding up to charge, or charging: the moment to sidestep. Only Dashers do.</summary>
        public virtual bool IsCharging => false;
        protected Rigidbody2D Body { get; private set; }
        /// <summary>The events of the arena this enemy was spawned into.</summary>
        protected EventChannel Events { get; private set; }
        protected Transform Target { get; private set; }
        /// <summary>The spawner this enemy came from, for enemies that spawn more. Null if it was placed by hand.</summary>
        protected EnemySpawner Spawner { get; private set; }
        protected bool HasTarget => Target != null && Target.gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
            Events = Arena.EventsFor(this);
            var sprite = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            _color = sprite != null ? sprite.color : Color.white;
        }

        void OnEnable()
        {
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void OnDisable()
        {
            Health.Damaged -= OnDamaged;
            Health.Died -= OnDied;
        }

        /// <summary>Called by the spawner each time this enemy is taken from the pool.</summary>
        public void Init(Transform target, Action<EnemyBase> release, EnemySpawner spawner = null)
        {
            Target = target;
            _release = release;
            Spawner = spawner;
            Health.ResetHealth(data.MaxHealth);
            Body.linearVelocity = Vector2.zero;
            OnSpawned();
        }

        /// <summary>Velocity the enemy steers toward this physics step. Only called while it has a target.</summary>
        protected abstract Vector2 DesiredVelocity();

        /// <summary>Hook for per-spawn setup in subclasses (timers, random choices). Runs at the end of <see cref="Init"/>.</summary>
        protected virtual void OnSpawned() { }

        /// <summary>Hook for what happens on death in subclasses (splitting). Runs after <see cref="EnemyKilled"/>, before returning to the pool.</summary>
        protected virtual void OnKilled() { }

        /// <summary>Units per second squared the body steers at this step. Defaults to the data's acceleration.</summary>
        protected virtual float Acceleration => data.Acceleration;

        /// <summary>Sets the velocity outright, e.g. to scatter enemies spawned on top of each other.</summary>
        public void Launch(Vector2 velocity) => Body.linearVelocity = velocity;

        /// <summary>Direction the visual should face. Defaults to the direction of travel.</summary>
        protected virtual Vector2 FacingDirection(Vector2 desiredVelocity) => desiredVelocity;

        protected virtual void FixedUpdate()
        {
            Vector2 desired = HasTarget ? DesiredVelocity() : Vector2.zero;
            Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, desired, Acceleration * Time.fixedDeltaTime);

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

        void OnDamaged(DamageInfo _) => Events.Publish(new EnemyDamaged(transform.position));

        void OnDied()
        {
            Events.Publish(new EnemyKilled(data.ScoreValue, transform.position, data.DropChance, _color));
            OnKilled();
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
