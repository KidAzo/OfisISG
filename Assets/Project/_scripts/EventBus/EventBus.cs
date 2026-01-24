using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.Events
{
	public interface IEvent { }

	public static class EventBus
	{
		// Her event type için listener listesi
		private class EventListeners<T> where T : IEvent
		{
			private readonly List<Action<T>> _listeners = new();
			private readonly List<Action<T>> _listenersToAdd = new();
			private readonly List<Action<T>> _listenersToRemove = new();
			private bool _isInvoking;

			public void Add(Action<T> callback)
			{
				if (callback == null) return;

				// Invoking sırasında ekleme - sonra ekle
				if (_isInvoking)
				{
					if (!_listenersToAdd.Contains(callback))
						_listenersToAdd.Add(callback);
					return;
				}

				// Duplicate check
				if (!_listeners.Contains(callback))
					_listeners.Add(callback);
			}

			public bool Remove(Action<T> callback)
			{
				if (callback == null) return false;

				// Invoking sırasında silme - sonra sil
				if (_isInvoking)
				{
					if (!_listenersToRemove.Contains(callback))
					{
						_listenersToRemove.Add(callback);
						return true;
					}
					return false;
				}

				return _listeners.Remove(callback);
			}

			public void Invoke(T evt)
			{
				if (_listeners.Count == 0) return;

				_isInvoking = true;

				// Listener'ları invoke et
				for (int i = 0; i < _listeners.Count; i++)
				{
					try
					{
						_listeners[i]?.Invoke(evt);
					}
					catch (Exception ex)
					{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
						Debug.LogError($"EventBus error in {typeof(T).Name}: {ex.Message}\n{ex.StackTrace}");
#endif
					}
				}

				_isInvoking = false;

				// Pending eklemeleri yap
				if (_listenersToAdd.Count > 0)
				{
					foreach (var listener in _listenersToAdd)
					{
						if (!_listeners.Contains(listener))
							_listeners.Add(listener);
					}
					_listenersToAdd.Clear();
				}

				// Pending silmeleri yap
				if (_listenersToRemove.Count > 0)
				{
					foreach (var listener in _listenersToRemove)
					{
						_listeners.Remove(listener);
					}
					_listenersToRemove.Clear();
				}
			}

			public int Count => _listeners.Count;
			
			public void Clear()
			{
				_listeners.Clear();
				_listenersToAdd.Clear();
				_listenersToRemove.Clear();
			}
		}

		// Type-specific listener storage
		private static readonly Dictionary<Type, object> _eventListeners = new();
		
		// Subscription handle pattern - otomatik dispose için
		public class Subscription : IDisposable
		{
			private Action _unsubscribeAction;
			private bool _disposed;

			public Subscription(Action unsubscribeAction)
			{
				_unsubscribeAction = unsubscribeAction;
			}

			public void Dispose()
			{
				if (_disposed) return;
				_disposed = true;
				_unsubscribeAction?.Invoke();
				_unsubscribeAction = null;
			}
		}

		/// <summary>
		/// Event'e subscribe ol. Dönen IDisposable ile otomatik unsubscribe yapabilirsin.
		/// </summary>
		public static Subscription Subscribe<T>(Action<T> callback) where T : IEvent
		{
			if (callback == null) 
			{
				Debug.LogWarning("EventBus: Null callback provided to Subscribe");
				return new Subscription(null);
			}

			GetOrCreateListeners<T>().Add(callback);
			
			return new Subscription(() => Unsubscribe(callback));
		}

		/// <summary>
		/// Event'ten unsubscribe ol
		/// </summary>
		public static bool Unsubscribe<T>(Action<T> callback) where T : IEvent
		{
			if (callback == null) return false;

			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				var result = ((EventListeners<T>)listeners).Remove(callback);
				
				// Eğer listener kalmadıysa dictionary'den kaldır (memory optimization)
				if (((EventListeners<T>)listeners).Count == 0)
				{
					_eventListeners.Remove(type);
				}
				
				return result;
			}
			return false;
		}

		/// <summary>
		/// Event publish et - tüm subscriber'lar çağrılır
		/// </summary>
		public static void Publish<T>(T evt) where T : IEvent
		{
			if (evt == null)
			{
				Debug.LogWarning($"EventBus: Null event of type {typeof(T).Name} published");
				return;
			}

			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				((EventListeners<T>)listeners).Invoke(evt);
			}
		}

		/// <summary>
		/// Belirli bir event type için listener sayısını al
		/// </summary>
		public static int GetListenerCount<T>() where T : IEvent
		{
			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				return ((EventListeners<T>)listeners).Count;
			}
			return 0;
		}

		/// <summary>
		/// Tüm event listener'ları temizle
		/// </summary>
		public static void Clear()
		{
			_eventListeners.Clear();
		}

		/// <summary>
		/// Belirli bir event type'ının tüm listener'larını temizle
		/// </summary>
		public static void Clear<T>() where T : IEvent
		{
			var type = typeof(T);
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				((EventListeners<T>)listeners).Clear();
				_eventListeners.Remove(type);
			}
		}

		private static EventListeners<T> GetOrCreateListeners<T>() where T : IEvent
		{
			var type = typeof(T);
			if (!_eventListeners.TryGetValue(type, out var listeners))
			{
				listeners = new EventListeners<T>();
				_eventListeners[type] = listeners;
			}
			return (EventListeners<T>)listeners;
		}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		/// <summary>
		/// Debug için event bus durumunu yazdır
		/// </summary>
		public static void PrintDebugInfo()
		{
			Debug.Log($"═══════════════════════════════════════");
			Debug.Log($"EventBus Debug Info");
			Debug.Log($"═══════════════════════════════════════");
			Debug.Log($"Active event types: {_eventListeners.Count}");
			
			foreach (var kvp in _eventListeners)
			{
				var listenerType = kvp.Value.GetType();
				var countProperty = listenerType.GetProperty("Count");
				var count = countProperty?.GetValue(kvp.Value);
				Debug.Log($"  • {kvp.Key.Name}: {count} listener(s)");
			}
			Debug.Log($"═══════════════════════════════════════");
		}

		/// <summary>
		/// Belirli bir event type için listener'ları listele
		/// </summary>
		public static void PrintListeners<T>() where T : IEvent
		{
			var type = typeof(T);
			Debug.Log($"Listeners for {type.Name}:");
			
			if (_eventListeners.TryGetValue(type, out var listeners))
			{
				var count = ((EventListeners<T>)listeners).Count;
				Debug.Log($"  Total: {count} listener(s)");
			}
			else
			{
				Debug.Log($"  No listeners registered");
			}
		}
#endif

		/// <summary>
		/// Unity domain reload'da otomatik temizlik
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Initialize()
		{
			Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Debug.Log("EventBus initialized and cleared");
#endif
		}
	}

	#region Helper Extensions
	/// <summary>
	/// MonoBehaviour için extension - otomatik unsubscribe
	/// </summary>
	public static class EventBusExtensions
	{
		private class SubscriptionTracker : MonoBehaviour
		{
			private readonly List<EventBus.Subscription> _subscriptions = new();
			private readonly List<EventBus.Subscription> _enableSubscriptions = new();

			public void Track(EventBus.Subscription subscription, bool disposeOnDisable)
			{
				if (disposeOnDisable)
					_enableSubscriptions.Add(subscription);
				else
					_subscriptions.Add(subscription);
			}

			private void OnDisable()
			{
				// OnDisable'da sadece enable subscriptions'ları temizle
				foreach (var sub in _enableSubscriptions)
				{
					sub?.Dispose();
				}
				_enableSubscriptions.Clear();
			}

			private void OnDestroy()
			{
				// OnDestroy'da her şeyi temizle
				foreach (var sub in _subscriptions)
				{
					sub?.Dispose();
				}
				_subscriptions.Clear();
				
				foreach (var sub in _enableSubscriptions)
				{
					sub?.Dispose();
				}
				_enableSubscriptions.Clear();
			}
		}

		/// <summary>
		/// OnDestroy'da otomatik unsubscribe (GameObject destroy olunca)
		/// Pooling kullanıyorsan bunu KULLANMA!
		/// </summary>
		public static void SubscribeWithCleanup<T>(this MonoBehaviour behaviour, Action<T> callback) where T : IEvent
		{
			var subscription = EventBus.Subscribe(callback);
			
			var tracker = behaviour.GetComponent<SubscriptionTracker>();
			if (tracker == null)
				tracker = behaviour.gameObject.AddComponent<SubscriptionTracker>();
			
			tracker.Track(subscription, disposeOnDisable: false);
		}

		/// <summary>
		/// OnDisable'da otomatik unsubscribe (GameObject deaktif olunca)
		/// Pooling/Menu sistemleri için bunu kullan!
		/// </summary>
		public static void SubscribeWhileEnabled<T>(this MonoBehaviour behaviour, Action<T> callback) where T : IEvent
		{
			var subscription = EventBus.Subscribe(callback);
			
			var tracker = behaviour.GetComponent<SubscriptionTracker>();
			if (tracker == null)
				tracker = behaviour.gameObject.AddComponent<SubscriptionTracker>();
			
			tracker.Track(subscription, disposeOnDisable: true);
		}
	}
	#endregion
}