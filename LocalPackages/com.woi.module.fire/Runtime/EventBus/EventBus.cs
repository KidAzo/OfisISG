using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.Events
{
    /// <summary>
    /// Lightweight static generic event bus.
    /// Events are value-type structs. Publish with <see cref="Raise{T}"/>,
    /// subscribe / unsubscribe with <see cref="Register{T}"/> / <see cref="Deregister{T}"/>.
    /// </summary>
    /// <remarks>
    /// Each unique event type <typeparamref name="T"/> gets its own isolated
    /// subscriber list. No ScriptableObject or MonoBehaviour required — any class
    /// can publish or listen.
    /// </remarks>
    public static class EventBus
    {
        // Maps event type → list of raw Action<object> wrappers.
        private static readonly Dictionary<Type, List<Delegate>> _bindings = new();

        // ── Subscribe ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers <paramref name="handler"/> to be called whenever
        /// an event of type <typeparamref name="T"/> is raised.
        /// </summary>
        public static void Register<T>(Action<T> handler) where T : struct
        {
            Type key = typeof(T);
            if (!_bindings.TryGetValue(key, out var list))
            {
                list = new List<Delegate>();
                _bindings[key] = list;
            }

            if (!list.Contains(handler))
                list.Add(handler);
        }

        // ── Unsubscribe ───────────────────────────────────────────────────────────

        /// <summary>
        /// Removes <paramref name="handler"/> from the subscriber list for
        /// event type <typeparamref name="T"/>.
        /// Safe to call even if the handler was never registered.
        /// </summary>
        public static void Deregister<T>(Action<T> handler) where T : struct
        {
            Type key = typeof(T);
            if (_bindings.TryGetValue(key, out var list))
                list.Remove(handler);
        }

        // ── Publish ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises an event of type <typeparamref name="T"/>, invoking all registered handlers.
        /// Handlers are called in registration order. Exceptions in one handler do not
        /// prevent subsequent handlers from running.
        /// </summary>
        public static void Raise<T>(T @event) where T : struct
        {
            Type key = typeof(T);
            if (!_bindings.TryGetValue(key, out var list) || list.Count == 0)
                return;

            // Iterate on a snapshot to allow handlers to safely deregister.
            var snapshot = new List<Delegate>(list);
            foreach (Delegate del in snapshot)
            {
                try
                {
                    ((Action<T>)del).Invoke(@event);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventBus] Exception in handler for {typeof(T).Name}: {ex}");
                }
            }
        }

        // ── Diagnostics ───────────────────────────────────────────────────────────

        /// <summary>
        /// Removes all subscribers for every event type.
        /// Call on scene unload to avoid stale references.
        /// </summary>
        public static void ClearAll() => _bindings.Clear();

        /// <summary>Returns the number of subscribers registered for event type <typeparamref name="T"/>.</summary>
        public static int SubscriberCount<T>() where T : struct
            => _bindings.TryGetValue(typeof(T), out var list) ? list.Count : 0;
    }
}
