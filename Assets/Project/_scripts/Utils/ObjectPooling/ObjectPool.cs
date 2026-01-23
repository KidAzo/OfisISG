using System;
using System.Collections.Generic;
using System.Linq;

namespace WoiUtils.Pooling
{
	public class ObjectPool<T> : IObjectPool<T>, IClearable where T : IPoolable
	{
		private readonly Stack<T> pool = new Stack<T>();

		public readonly HashSet<T> ActiveObjects = new();

		private readonly Func<T> createFunc;
		private readonly Action<T> onGet;
		private readonly Action<T> onRelease;
		private readonly Action<T> onDestroy;

		private int? maxSize;

		public int CountInactive => pool.Count;
		public int CountActive => ActiveObjects.Count;
		public int CountAll => pool.Count + ActiveObjects.Count;

		public ObjectPool(
			Func<T> createFunc,
			Action<T> onGet = null,
			Action<T> onRelease = null,
			Action<T> onDestroy = null,
			int defaultCapacity = 0,
			int? maxSize = null)
		{
			this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
			this.onGet = onGet;
			this.onRelease = onRelease;
			this.onDestroy = onDestroy;

			this.maxSize = maxSize;

			pool = new Stack<T>(defaultCapacity);
			
			Prewarm(defaultCapacity);
		}

		public void Prewarm(int count)
		{
			for (int i = 0; i < count; i++)
			{
				var item = createFunc();
				ReturnToPool(item);	
			}
		}

		public T Get()
		{
			T item = pool.Count > 0 ? pool.Pop() : createFunc();

			ActiveObjects.Add(item);
			onGet?.Invoke(item);

			return item;
		}

		public void ReturnToPool(T item)
		{
			onRelease?.Invoke(item);

			if (ActiveObjects.Contains(item))
				ActiveObjects.Remove(item);

			if (maxSize.HasValue && pool.Count >= maxSize.Value)
			{
				onDestroy?.Invoke(item);
				return;
			}

			pool.Push(item);
		}

		public void ReturnAllActive()
		{
			var activeCopy = ActiveObjects.ToArray();

			foreach (var item in activeCopy)
				ReturnToPool(item);
		}

		public void Clear(bool destroyObjects = false)
		{
			if (destroyObjects && onDestroy != null)
			{
				foreach (var item in pool)
					onDestroy(item);
			}

			pool.Clear();
			ActiveObjects.Clear();
		}
	}	
}

