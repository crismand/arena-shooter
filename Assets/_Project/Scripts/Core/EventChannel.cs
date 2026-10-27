using System;
using System.Collections.Generic;

namespace AS.Core
{
    /// <summary>
    /// Type-safe event channel. Systems publish event structs (see GameEvents.cs) and listeners
    /// subscribe by type, so gameplay systems never reference each other directly. A channel can
    /// forward everything published on it to another channel, after its own listeners have run.
    /// </summary>
    public sealed class EventChannel
    {
        readonly Dictionary<Type, Delegate> _handlers = new();
        readonly EventChannel _forwardTo;

        public EventChannel(EventChannel forwardTo = null) => _forwardTo = forwardTo;

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            _handlers.TryGetValue(typeof(T), out var existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = remaining;
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler).Invoke(evt);
            _forwardTo?.Publish(evt);
        }

        public void Clear() => _handlers.Clear();
    }
}
