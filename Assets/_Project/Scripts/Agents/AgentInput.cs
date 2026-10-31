using AS.Core;
using UnityEngine;

namespace AS.Agents
{
    /// <summary>
    /// Player commands set by an <see cref="ArenaAgent"/> instead of read from a device. Holds each
    /// action until the next, so the player keeps moving and firing between decisions. Runs before
    /// PlayerAim, like HumanPlayerInput, so the aim is current.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class AgentInput : MonoBehaviour, IPlayerInput
    {
        /// <summary>Aim actions shorter than this keep the previous aim, so the barrel doesn't spin around a zero vector.</summary>
        public const float MinAim = 0.1f;

        bool _hasAim;
        Vector2 _aim = Vector2.right;

        public Vector2 Move { get; private set; }
        public bool FireHeld { get; private set; }

        public bool TryGetAim(out Vector2 direction)
        {
            direction = _aim;
            return _hasAim;
        }

        /// <summary>Takes one action. <paramref name="move"/> is clamped to the unit circle and <paramref name="aim"/> normalised.</summary>
        public void Apply(Vector2 move, Vector2 aim, bool fire)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            FireHeld = fire;
            _hasAim = aim.sqrMagnitude >= MinAim * MinAim;
            if (_hasAim) _aim = aim.normalized;
        }

        /// <summary>Stand still, hold fire and keep the current aim.</summary>
        public void Clear() => Apply(Vector2.zero, Vector2.zero, false);
    }
}
