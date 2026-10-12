using System;
using AS.Combat;
using UnityEngine;

namespace AS.Enemies
{
    /// <summary>
    /// Shared enemy behaviour: steering toward a desired velocity, facing its movement, damaging
    /// whatever it touches on <see cref="contactTargets"/>, and returning to its pool on death.
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
        }

        /// <summary>Velocity the enemy steers toward this physics step. Only called while it has a target.</summary>
        protected abstract Vector2 DesiredVelocity();

        protected virtual void FixedUpdate()
        {
            Vector2 desired = HasTarget ? DesiredVelocity() : Vector2.zero;
            Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, desired, data.Acceleration * Time.fixedDeltaTime);

            if (visual != null && desired.sqrMagnitude > 0.01f)
                visual.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if ((contactTargets.value & (1 << collision.gameObject.layer)) == 0) return;
            Vector2 direction = ((Vector2)(collision.transform.position - transform.position)).normalized;
            collision.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo(data.ContactDamage, direction, gameObject));
        }

        void OnDied()
        {
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
