using System;
using UnityEngine;

namespace Woi.Settings
{

	public class LoadingProgress : IProgress<float>
	{
		public event Action<float> Progressed;

		const float ratio = 1.0f;

		public void Report(float value)
		{
			Progressed?.Invoke(value / ratio);
			Debug.Log(value * 100);
		}
	}
}




