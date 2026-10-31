using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AS.Core
{
    /// <summary>
    /// Wraps the Input System actions asset so gameplay code never touches devices directly.
    /// Expected actions in the "Player" map: Move, Aim (right stick), Point (mouse position),
    /// Attack, Pause. Enabled and disabled by <see cref="GameManager"/>.
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
        InputAction _pause;

        public event Action PausePressed;

        Action _anyButtonPressed;

        /// <summary>
        /// Any button on any device pressed: keys, mouse buttons, gamepad buttons and triggers, but not
        /// mouse movement. Ignores <see cref="GameplayEnabled"/>. Only listens to the Input System while
        /// something is subscribed.
        /// </summary>
        public event Action AnyButtonPressed
        {
            add
            {
                if (_anyButtonPressed == null) InputSystem.onEvent += OnInputEvent;
                _anyButtonPressed += value;
            }
            remove
            {
                _anyButtonPressed -= value;
                if (_anyButtonPressed == null) InputSystem.onEvent -= OnInputEvent;
            }
        }

        /// <summary>When false, movement, aim and fire read as idle (pause still fires).</summary>
        public bool GameplayEnabled { get; set; } = true;

        public Vector2 Move => GameplayEnabled ? Read(_move) : Vector2.zero;
        /// <summary>Gamepad right stick, -1..1.</summary>
        public Vector2 AimStick => GameplayEnabled ? Read(_aim) : Vector2.zero;
        /// <summary>Mouse position in screen pixels.</summary>
        public Vector2 PointerPosition => Read(_point);
        public bool FireHeld => GameplayEnabled && _fire != null && _fire.IsPressed();

        public void Enable()
        {
            if (_map != null) return;
            if (actions == null)
            {
                Debug.LogError($"[InputReader] No InputActionAsset assigned on {name}.", this);
                return;
            }

            // The asset outlives scene reloads; a restart from pause must not leave gameplay off.
            GameplayEnabled = true;

            _map = actions.FindActionMap(playerMapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _aim = _map.FindAction("Aim", throwIfNotFound: true);
            _point = _map.FindAction("Point", throwIfNotFound: true);
            _fire = _map.FindAction("Attack", throwIfNotFound: true);
            _pause = _map.FindAction("Pause", throwIfNotFound: true);
            _pause.performed += OnPause;
            _map.Enable();
        }

        public void Disable()
        {
            if (_pause != null) _pause.performed -= OnPause;
            _map?.Disable();
            _map = null;
            _move = _aim = _point = _fire = _pause = null;
        }

        void OnInputEvent(InputEventPtr eventPtr, InputDevice _)
        {
            if (eventPtr.GetFirstButtonPressOrNull() != null) _anyButtonPressed?.Invoke();
        }

        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();

        static Vector2 Read(InputAction action) => action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }
}
