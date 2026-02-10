using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Woi.HazardSystem
{
	public class ToggleObjectsHazard : Hazard
	{
		[SerializeField] ToggleObjectsConfig config;
		public ToggleObjectsConfig Config => config;
		public UnityEvent onStart;
		public UnityEvent onComplete;

		protected override void BuildOperations()
		{
			HazardOperationFactory.Build(
				HazardType.ToggleObjects,
				config,
				this,
				operations,	
				RaiseOnComplete,
				RaiseOnStart
			);
		}

		void RaiseOnComplete()
		{
			onComplete?.Invoke();
		}	

		void RaiseOnStart()
		{
			onStart?.Invoke();
		}
	}
}
