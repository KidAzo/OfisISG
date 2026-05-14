using UnityEngine;

namespace WoiUtils.Pooling
{
	[RequireComponent(typeof(ParticleSystem))]
	public class ParticleAutoReturn : AutoReturnToPool<ParticleAutoReturn>, IPoolable
	{
		private ParticleSystem particle;

		private void Awake() => particle = GetComponent<ParticleSystem>();

		public void Get() => particle.Play(true);
		public void Release() => particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	}
}