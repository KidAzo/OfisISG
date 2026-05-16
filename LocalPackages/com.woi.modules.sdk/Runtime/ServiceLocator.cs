using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace WOI.Modules.SDK
{
    /// <summary>
    /// Thread-safe static service registry for module runtime. Call <see cref="Clear"/> when unloading a module.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly ConcurrentDictionary<Type, object> Services = new();

        /// <summary>Fired after <see cref="Clear"/> completes.</summary>
        public static event Action Cleared;

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var key = typeof(T);
            if (!Services.TryAdd(key, service))
            {
                Debug.LogError($"[ServiceLocator] Service already registered for {key.FullName}. Call Unregister<{typeof(T).Name}>() or Clear() before registering again.");
            }
        }

        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out var service))
                return service;

            Debug.LogError($"[ServiceLocator] No service registered for {typeof(T).FullName}.");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public static bool Unregister<T>() where T : class
        {
            return Services.TryRemove(typeof(T), out _);
        }

        public static void Clear()
        {
            Services.Clear();
            Cleared?.Invoke();
        }

        public static bool IsRegistered<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }
    }
}
