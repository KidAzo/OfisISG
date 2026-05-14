namespace WoiUtils.Pooling
{
	public interface IObjectPool<T> where T : IPoolable
	{
		T Get();
		void ReturnToPool(T item);
		void ReturnAllActive();
	}
}

