using System;
using System.Collections.Generic;
using UnityEngine;

namespace AS.Core
{
    /// <summary>
    /// Type-safe global event bus. Systems publish event structs (see GameEvents.cs) and
    /// listeners subscribe by type, so gameplay systems never reference each other directly.
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> Handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Handlers.TryGetValue(typeof(T), out var existing);
            Handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) Handlers.Remove(typeof(T));
            else Handlers[typeof(T)] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (Handlers.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler).Invoke(evt);
        }

        // Statics survive play sessions when domain reload is disabled; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Handlers.Clear();
    }
}
