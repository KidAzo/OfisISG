using System;
using UnityEngine;

namespace HazardSystem
{
    public class RotationObjectHazard : Hazard
	{
		[SerializeField] RotationObjectConfig config;

		protected override void BuildOperations()
		{
			HazardOperationFactory.Build(
				HazardType.Rotation,
				config,
				this,
				operations
			);
		}
	}
}
