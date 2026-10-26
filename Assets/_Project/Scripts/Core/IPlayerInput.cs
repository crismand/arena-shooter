using UnityEngine;

namespace AS.Core
{
    /// <summary>
    /// Where the player's commands come from: a person through the Input System, or an agent.
    /// Read once per frame by the player scripts, so implementations should be current by the time
    /// <c>Update</c> runs at the default execution order.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Desired movement, magnitude up to 1.</summary>
        Vector2 Move { get; }

        bool FireHeld { get; }

        /// <summary>
        /// True with a unit direction while this source is aiming. False keeps the previous aim, so
        /// the barrel doesn't snap back when the stick or mouse goes idle.
        /// </summary>
        bool TryGetAim(out Vector2 direction);
    }
}
