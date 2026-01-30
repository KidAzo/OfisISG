using UnityEngine;

namespace HazardSystem
{
	public class ToggleObjectsHazard : Hazard
	{
		[SerializeField] ToggleObjectsConfig config;

		protected override void BuildOperations()
		{
			HazardOperationFactory.Build(
				HazardType.ToggleObjects,
				config,
				this,
				operations
			);
		}
	}
}
