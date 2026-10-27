using UnityEngine;

namespace AS.Core
{
    /// <summary>
    /// Root of one playable arena. Gameplay objects under it (the player, spawner, wave director and
    /// pickup dropper, and the enemies, bullets and pickups they pool) publish and listen on its
    /// <see cref="Events"/>, so several arenas can share a scene without hearing each other. A
    /// forwarding arena also republishes everything on the global <see cref="EventBus"/>, where the
    /// HUD, effects and game manager listen.
    /// </summary>
    public class Arena : MonoBehaviour
    {
        [Tooltip("Republish this arena's events on the global EventBus. Turn off for arenas nobody watches, such as extra training arenas.")]
        [SerializeField] bool forwardToGlobal = true;

        EventChannel _events;

        // Created on first use: children look it up in their own Awake, which can run before ours.
        public EventChannel Events => _events ??= new EventChannel(forwardToGlobal ? EventBus.Global : null);

        /// <summary>
        /// Starts the arena over without reloading the scene: the player is restored, enemies,
        /// bullets and pickups go back to their pools, and the waves begin again from wave 1.
        /// Each system does its own part when it hears <see cref="ArenaReset"/>.
        /// </summary>
        public void ResetArena() => Events.Publish(new ArenaReset());

        /// <summary>The arena <paramref name="component"/> is in, or null if it isn't under one.</summary>
        public static Arena Of(Component component) => component.GetComponentInParent<Arena>(includeInactive: true);

        /// <summary>The events of the arena <paramref name="component"/> is in, or the global channel outside any arena.</summary>
        public static EventChannel EventsFor(Component component)
        {
            var arena = Of(component);
            return arena != null ? arena.Events : EventBus.Global;
        }
    }
}
