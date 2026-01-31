using HazardSystem;
using UnityEngine;

namespace Woi.HazardSystem
{
	public class EventHazard : Hazard
	{
		[SerializeField] EventConfig config;
		
		protected override void BuildOperations()
		{
			HazardOperationFactory.Build(
				HazardType.Event,
				config,
				this,
				operations
			);
		}
	}	
}

