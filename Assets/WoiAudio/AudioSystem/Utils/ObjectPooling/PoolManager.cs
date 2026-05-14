using System.Collections.Generic;
using System;
using UnityEngine;
using WoiUtils.Pooling.Utils.Pooling;

namespace WoiUtils.Pooling
{
	public static class PoolManager
	{
		private static readonly Dictionary<Type, object> _pools = new();

		static PoolManager()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
		}

#if UNITY_EDITOR
		private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
		{
			if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
			{
				ClearAll();
			}
		}
#endif

		public static IObjectPool<T> Register<T>(
			Func<T> createFunc,
			Action<T> onGet = null,
			Action<T> onRelease = null,
			Action<T> onDestroy = null,
			int defaultCapacity = 0,
			int? maxSize = null
		) where T : IPoolable
		{
			Type type = typeof(T);

			if (_pools.TryGetValue(type, out var existingPool))
			{
				Debug.LogWarning($"Pool for type {type.Name} already exists. Returning existing pool.");
				ReturnAll<T>();
				return (ObjectPool<T>)existingPool;
			}

			var pool = new ObjectPool<T>(
				createFunc,
				onGet,
				onRelease,
				onDestroy,
				defaultCapacity,
				maxSize
			);

			_pools[type] = pool;
			
			return pool;
		}

		public static T Get<T>() where T : IPoolable
		{
			if (!_pools.TryGetValue(typeof(T), out var pool))
				throw new InvalidOperationException($"No pool registered for type {typeof(T).Name}");

			return ((IObjectPool<T>)pool).Get();
		}

		public static void Return<T>(T item) where T : IPoolable
		{
			if (!_pools.TryGetValue(typeof(T), out var pool))
				throw new InvalidOperationException($"No pool registered for type {typeof(T).Name}");

			((IObjectPool<T>)pool).ReturnToPool(item);
		}

		public static void ReturnAll<T>() where T : IPoolable
		{
			if (_pools.TryGetValue(typeof(T), out var poolObj))
			{
				var pool = (IObjectPool<T>)poolObj;
				pool.ReturnAllActive();
			}
		}

		public static void Clear<T>() where T : IPoolable
		{
			if (_pools.TryGetValue(typeof(T), out var pool))
			{
				((ObjectPool<T>)pool).Clear(true);
				_pools.Remove(typeof(T));
			}
		}

		public static void ClearAll()
		{
			foreach (var pool in _pools.Values)
			{
				if (pool is IClearable clearable)
					clearable.Clear(true);
			}

			_pools.Clear();

			if (PoolingRootManager.root != null)
			{
#if UNITY_EDITOR
				if (!UnityEditor.EditorApplication.isPlaying)
					GameObject.DestroyImmediate(PoolingRootManager.root);
				else
					GameObject.Destroy(PoolingRootManager.root);
#else
				GameObject.Destroy(PoolingRootManager.root);
#endif

				PoolingRootManager.SubParents.Clear();
			}
		}
	}
}

