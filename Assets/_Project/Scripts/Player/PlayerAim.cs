using AS.Core;
using UnityEngine;

namespace AS.Player
{
    /// <summary>
    /// Holds the direction the player is aiming, taken from its <see cref="IPlayerInput"/>, and rotates
    /// <see cref="pivot"/> to match. Runs before other player scripts so the direction is current
    /// when the weapon fires.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PlayerAim : MonoBehaviour
    {
        [SerializeField] Transform pivot;

        /// <summary>Defaults to the <see cref="IPlayerInput"/> on this object; set it to hand control elsewhere.</summary>
        public IPlayerInput InputSource { get; set; }

        public Vector2 Direction { get; private set; } = Vector2.right;

        void Awake()
        {
            if (InputSource == null) InputSource = GetComponent<IPlayerInput>();
        }

        void Update()
        {
            if (InputSource != null && InputSource.TryGetAim(out Vector2 direction)) Direction = direction;
            if (pivot != null) pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }
    }
}
