using AS.Core;
using UnityEngine;

namespace AS.Player
{
    /// <summary>
    /// Player commands from keyboard, mouse and gamepad. Aims with the right stick when it's pushed,
    /// otherwise at the mouse once it has moved, and fires while the trigger is held or the stick is
    /// pushed (twin-stick auto-fire). Runs before <see cref="PlayerAim"/> so the aim is current.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class HumanPlayerInput : MonoBehaviour, IPlayerInput
    {
        [SerializeField] InputReader input;
        [SerializeField, Range(0f, 1f)] float stickDeadZone = 0.25f;

        Camera _camera;
        Vector2 _lastPointer;
        bool _pointerAiming;
        bool _stickAiming;
        bool _hasAim;
        Vector2 _aim;

        public Vector2 Move => input.Move;
        public bool FireHeld => input.FireHeld || _stickAiming;

        public bool TryGetAim(out Vector2 direction)
        {
            direction = _aim;
            return _hasAim;
        }

        void Start()
        {
            _camera = Camera.main;
            _lastPointer = input.PointerPosition;
        }

        void Update()
        {
            Vector2 stick = input.AimStick;
            Vector2 pointer = input.PointerPosition;
            _stickAiming = stick.sqrMagnitude > stickDeadZone * stickDeadZone;
            _hasAim = false;

            if (_stickAiming)
            {
                _pointerAiming = false;
                SetAim(stick.normalized);
            }
            else
            {
                if ((pointer - _lastPointer).sqrMagnitude > 1f) _pointerAiming = true;
                if (_pointerAiming && _camera != null)
                {
                    Vector2 toPointer = (Vector2)_camera.ScreenToWorldPoint(pointer) - (Vector2)transform.position;
                    if (toPointer.sqrMagnitude > 0.01f) SetAim(toPointer.normalized);
                }
            }
            _lastPointer = pointer;
        }

        void SetAim(Vector2 direction)
        {
            _aim = direction;
            _hasAim = true;
        }
    }
}
