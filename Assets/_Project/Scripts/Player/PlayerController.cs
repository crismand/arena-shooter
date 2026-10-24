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
        // Read in Update, used in FixedUpdate. Reading input inside FixedUpdate isn't reliable: on
        // the Linux editor (CI) a WASD composite read there came back zero while Update saw it held.
        Vector2 _moveInput;

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
            _moveInput = Vector2.ClampMagnitude(input.Move, 1f);
            if ((input.FireHeld || aim.IsStickAiming) && weapon.TryFire(aim.Direction))
                EventBus.Publish(new ShotFired(transform.position, byPlayer: true));
        }

        void FixedUpdate()
        {
            Vector2 target = _moveInput * moveSpeed;
            _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }

        void OnDamaged(DamageInfo _)
        {
            PublishHealth();
            EventBus.Publish(new PlayerDamaged(transform.position));
        }

        void OnDied()
        {
            EventBus.Publish(new PlayerDied(transform.position));
            gameObject.SetActive(false);
        }

        void PublishHealth() => EventBus.Publish(new PlayerHealthChanged(health.Current, health.Max));
    }
}
