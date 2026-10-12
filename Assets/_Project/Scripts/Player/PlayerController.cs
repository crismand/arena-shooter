using AS.Combat;
using AS.Core;
using UnityEngine;

namespace AS.Player
{
    /// <summary>Eight-way movement plus firing while the trigger is held or the aim stick is pushed.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] PlayerAim aim;
        [SerializeField] Weapon weapon;
        [SerializeField, Min(0f)] float moveSpeed = 7f;
        [Tooltip("Units per second squared. Higher feels snappier.")]
        [SerializeField, Min(0f)] float acceleration = 70f;

        Rigidbody2D _body;

        void Awake() => _body = GetComponent<Rigidbody2D>();
        void OnEnable() => input.Enable();
        void OnDisable() => input.Disable();

        void Update()
        {
            if (input.FireHeld || aim.IsStickAiming) weapon.TryFire(aim.Direction);
        }

        void FixedUpdate()
        {
            Vector2 target = Vector2.ClampMagnitude(input.Move, 1f) * moveSpeed;
            _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, target, acceleration * Time.fixedDeltaTime);
        }
    }
}
