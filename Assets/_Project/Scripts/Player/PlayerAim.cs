using AS.Core;
using UnityEngine;

namespace AS.Player
{
    /// <summary>
    /// Works out where the player is aiming: the right stick when it's pushed, otherwise the mouse
    /// once it has moved. Rotates <see cref="pivot"/> to match. Runs before other player scripts so
    /// the direction is current when the weapon fires.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PlayerAim : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] Transform pivot;
        [SerializeField, Range(0f, 1f)] float stickDeadZone = 0.25f;

        Camera _camera;
        Vector2 _lastPointer;
        bool _pointerAiming;

        public Vector2 Direction { get; private set; } = Vector2.right;
        /// <summary>True while the right stick is pushed past the dead zone (twin-stick auto-fire).</summary>
        public bool IsStickAiming { get; private set; }

        void Start()
        {
            _camera = Camera.main;
            _lastPointer = input.PointerPosition;
        }

        void Update()
        {
            Vector2 stick = input.AimStick;
            Vector2 pointer = input.PointerPosition;
            IsStickAiming = stick.sqrMagnitude > stickDeadZone * stickDeadZone;

            if (IsStickAiming)
            {
                _pointerAiming = false;
                Direction = stick.normalized;
            }
            else
            {
                if ((pointer - _lastPointer).sqrMagnitude > 1f) _pointerAiming = true;
                if (_pointerAiming && _camera != null)
                {
                    Vector2 toPointer = (Vector2)_camera.ScreenToWorldPoint(pointer) - (Vector2)transform.position;
                    if (toPointer.sqrMagnitude > 0.01f) Direction = toPointer.normalized;
                }
            }
            _lastPointer = pointer;

            if (pivot != null) pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }
    }
}
