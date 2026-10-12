using UnityEngine;
using UnityEngine.InputSystem;

namespace AS.Core
{
    /// <summary>
    /// Wraps the Input System actions asset so gameplay code never touches devices directly.
    /// Expected actions in the "Player" map: Move, Aim (right stick), Point (mouse position), Attack.
    /// </summary>
    [CreateAssetMenu(menuName = "AS/Input Reader", fileName = "InputReader")]
    public class InputReader : ScriptableObject
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] string playerMapName = "Player";

        InputActionMap _map;
        InputAction _move;
        InputAction _aim;
        InputAction _point;
        InputAction _fire;

        public Vector2 Move => Read(_move);
        /// <summary>Gamepad right stick, -1..1.</summary>
        public Vector2 AimStick => Read(_aim);
        /// <summary>Mouse position in screen pixels.</summary>
        public Vector2 PointerPosition => Read(_point);
        public bool FireHeld => _fire != null && _fire.IsPressed();

        public void Enable()
        {
            if (_map != null) return;
            if (actions == null)
            {
                Debug.LogError($"[InputReader] No InputActionAsset assigned on {name}.", this);
                return;
            }

            _map = actions.FindActionMap(playerMapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _aim = _map.FindAction("Aim", throwIfNotFound: true);
            _point = _map.FindAction("Point", throwIfNotFound: true);
            _fire = _map.FindAction("Attack", throwIfNotFound: true);
            _map.Enable();
        }

        public void Disable()
        {
            _map?.Disable();
            _map = null;
            _move = _aim = _point = _fire = null;
        }

        static Vector2 Read(InputAction action) => action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }
}
