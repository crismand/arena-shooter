using System;
using UnityEngine;

namespace AS.Core
{
    /// <summary>
    /// The global event channel, for app-level events (game state, score, high score) and for
    /// everything a forwarding <see cref="Arena"/> republishes. The HUD, menus, effects and the game
    /// manager listen here. Gameplay inside an arena publishes on <see cref="Arena.Events"/> instead.
    /// </summary>
    public static class EventBus
    {
        public static EventChannel Global { get; } = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct => Global.Subscribe(handler);
        public static void Unsubscribe<T>(Action<T> handler) where T : struct => Global.Unsubscribe(handler);
        public static void Publish<T>(T evt) where T : struct => Global.Publish(evt);

        // Statics survive play sessions when domain reload is disabled; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Global.Clear();
    }
}
