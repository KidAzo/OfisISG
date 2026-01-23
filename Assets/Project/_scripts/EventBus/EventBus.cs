using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.Events
{
	public interface IEvent { }

	public static class EventBus
	{
		// Delegate cache to avoid boxing and reflection
		private class EventListeners<T> where T : IEvent
		{
			private readonly List<WeakActionRef<T>> _listeners = new();
			private readonly List<WeakActionRef<T>> _toRemove = new(); // Reuse list to avoid GC

			public void Add(Action<T> callback)
			{
				// Check for duplicates
				var target = callback.Target;
				var method = callback.Method;

				for (int i = 0; i < _listeners.Count; i++)
				{
					if (_listeners[i].Matches(target, method))
						return; // Already subscribed
				}

				_listeners.Add(new WeakActionRef<T>(callback));
			}

			public bool Remove(Action<T> callback)
			{
				var target = callback.Target;
				var method = callback.Method;

				for (int i = 0; i < _listeners.Count; i++)
				{
					if (_listeners[i].Matches(target, method))
					{
						_listeners.RemoveAt(i);
						return true;
					}
				}
				return false;
			}

			public void Invoke(T evt)
			{
				if (_listeners.Count == 0) return;

				_toRemove.Clear();

				// Process listeners
				for (int i = 0; i < _listeners.Count; i++)
				{
					var listener = _listeners[i];
					if (!listener.TryInvoke(evt))
					{
						_toRemove.Add(listener);
					}
				}

				// Remove dead references
				if (_toRemove.Count > 0)
				{
					for (int i = 0; i < _toRemove.Count; i++)
					{
						_listeners.Remove(_toRemove[i]);
					}
				}
			}

			public int Count => _listeners.Count;
			public void Clear() => _listeners.Clear();
		}

		private class WeakActionRef<T> where T : IEvent
		{
			private readonly WeakReference _targetRef;
			private readonly string _methodName;
			private readonly bool _isStatic;
			private Action<T> _cachedAction; // For static methods

			public WeakActionRef(Action<T> action)
			{
				_isStatic = action.Target == null;
				_methodName = action.Method.Name;

				if (_isStatic)
				{
					_cachedAction = action; // Static methods don't need weak ref
				}
				else
				{
					_targetRef = new WeakReference(action.Target);
				}
			}

			public bool TryInvoke(T evt)
			{
				try
				{
					if (_isStatic)
					{
						_cachedAction?.Invoke(evt);
						return true;
					}

					if (_targetRef?.Target != null)
					{
						// Recreate action - still faster than reflection
						var target = _targetRef.Target;
						var action = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, _methodName);
						action.Invoke(evt);
						return true;
					}

					return false; // Dead reference
				}
				catch (Exception ex)
				{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
					Debug.LogError($"EventBus error in {_methodName}: {ex.Message}");
#endif
					return _isStatic; // Keep static methods even if they error
				}
			}

			public bool Matches(object target, System.Reflection.MethodInfo method)
			{
				if (_isStatic)
					return target == null && method.Name == _methodName;

				return _targetRef?.Target == target && method.Name == _methodName;
			}

			public bool IsAlive => _isStatic || (_targetRef?.IsAlive ?? false);
		}

		// Type-specific listener storage
		private static readonly Dictionary<Type, object> _eventListeners = new();

		public static void Subscribe<T>(Action<T> callback) where T : IEvent
		{
			if (callback == null) return;

			var type = typeof(T);
			if (!_eventListeners.TryGetValue(type, out var listeners))
			{
				listeners = new EventListeners<T>();
				_eventListeners[type] = listeners;
			}

			((EventListeners<T>)listeners).Add(callback);
		}

		public static bool Unsubscribe<T>(Action<T> callback) where T : IEvent
		{
			if (callback == null) return false;

			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				return ((EventListeners<T>)listeners).Remove(callback);
			}
			return false;
		}

		public static void Publish<T>(T evt) where T : IEvent
		{
			if (evt == null) return;

			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				((EventListeners<T>)listeners).Invoke(evt);
			}
		}

		public static int GetListenerCount<T>() where T : IEvent
		{
			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				return ((EventListeners<T>)listeners).Count;
			}
			return 0;
		}

		public static void Clear()
		{
			_eventListeners.Clear();
		}

		// Clean up specific event type
		public static void Clear<T>() where T : IEvent
		{
			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				((EventListeners<T>)listeners).Clear();
			}
		}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		public static void PrintDebugInfo()
		{
			Debug.Log($"EventBus - Active event types: {_eventListeners.Count}");
			foreach (var kvp in _eventListeners)
			{
				var count = kvp.Value.GetType().GetMethod("get_Count")?.Invoke(kvp.Value, null);
				Debug.Log($"  {kvp.Key.Name}: {count} listeners");
			}
		}
#endif

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Initialize()
		{
			Clear();
		}
	}
}
