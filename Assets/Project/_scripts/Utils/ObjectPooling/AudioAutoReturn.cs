using UnityEngine;

namespace WoiUtils.Pooling
{
	[RequireComponent(typeof(AudioSource))]
	public class AudioAutoReturn : AutoReturnToPool<AudioAutoReturn>, IPoolable, IAutoReturnDelay
	{
		private AudioSource audioSource;

		private void Awake() => audioSource = GetComponent<AudioSource>();

		public float GetDelay() => audioSource.clip.length;
		public void Get() => audioSource.Play();
		public void Release() => audioSource.Stop();
	}
}