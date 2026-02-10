using UnityEngine;
using UnityEngine.UI;

namespace Woi.HazardSystem
{
	public class ToggleObjectsHazard : Hazard
	{
		[SerializeField] ToggleObjectsConfig config;
		public ToggleObjectsConfig Config => config;

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
