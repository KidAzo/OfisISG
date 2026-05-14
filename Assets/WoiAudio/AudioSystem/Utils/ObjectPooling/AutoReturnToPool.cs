using UnityEngine;
using System;

namespace WoiUtils.Pooling
{
	public class AutoReturnToPool<T> : MonoBehaviour where T : MonoBehaviour, IPoolable
	{
		[SerializeField] protected float returnDelay = 1f;
		[SerializeField] protected bool autoStartOnEnable = true;

		private Coroutine _routine;

		protected virtual void OnEnable()
		{
			if (autoStartOnEnable)
				StartAutoReturn(returnDelay);
		}

		protected virtual void OnDisable()
		{
			Cancel();
		}

		private void Cancel()
		{
			if (_routine != null)
			{
				StopCoroutine(_routine);
				_routine = null;
			}
		}

		public void StartAutoReturn(float delay)
		{
			Cancel();
			_routine = StartCoroutine(DelayAndReturn(delay));
		}

		private System.Collections.IEnumerator DelayAndReturn(float delay)
		{
			if (delay > 0f)
				yield return new WaitForSeconds(delay);
			else
				yield return null; // avoid returning in the same frame

			// Safety when returning object to pool:
			// - check if component still exists
			// - check if obj is active (you can remove this based on your pooling logic)
			if (this && gameObject.activeInHierarchy)
			{
				var t = this as T;
				if (t != null)
					PoolManager.Return(t);
			}

			_routine = null;
		}
	}
}
