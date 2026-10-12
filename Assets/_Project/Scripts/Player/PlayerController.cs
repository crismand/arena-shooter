using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Player
{
    /// <summary>
    /// Eight-way movement plus firing while the trigger is held or the aim stick is pushed.
    /// Reports its health on the event bus and leaves play when it runs out.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] PlayerAim aim;
        [SerializeField] Weapon weapon;
        [SerializeField] Health health;
        [SerializeField, Min(0f)] float moveSpeed = 7f;
        [Tooltip("Units per second squared. Higher feels snappier.")]
        [SerializeField, Min(0f)] float acceleration = 70f;

        Rigidbody2D _body;

        void Awake() => _body = GetComponent<Rigidbody2D>();

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void Start() => PublishHealth();

        void Update()
        {
            if (input.FireHeld || aim.IsStickAiming) weapon.TryFire(aim.Direction);
        }

        void FixedUpdate()
        {
            Vector2 target = Vector2.ClampMagnitude(input.Move, 1f) * moveSpeed;
            _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }

        void OnDamaged(DamageInfo _) => PublishHealth();

        void OnDied()
        {
            EventBus.Publish(new PlayerDied());
            gameObject.SetActive(false);
        }

        void PublishHealth() => EventBus.Publish(new PlayerHealthChanged(health.Current, health.Max));
    }
}
