namespace WoiUtils.Pooling
{
	public interface IPoolable 
	{
		void Get();
		void Release();
	}
}
