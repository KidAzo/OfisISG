using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HazardSystem
{
	public static class HazardOperationFactory
	{
		private static readonly Dictionary<HazardType, object> builders =
			new Dictionary<HazardType, object>
		{
			{ HazardType.ToggleObjects, new ToggleObjectsOperationBuilder() },
			{ HazardType.Event, new EventOperationBuilder() },
		};

		public static void Build<TConfig>(
			  HazardType type,
			  TConfig config,
			  Hazard hazard,
			  List<IHazardOperation> ops)
		{
			if (builders.TryGetValue(type, out var builderObj))
			{
				var builder = builderObj as IHazardOperationBuilder<TConfig>;
				if (builder == null)
				{
					Debug.LogError($"Wrong config type for {type}");
					return;
				}

				builder.Build(config, hazard, ops);
			}
			else
			{
				Debug.LogError($"No builder for hazard type: {type}");
			}
		}
	}

	public class ToggleObjectsOperationBuilder : IHazardOperationBuilder<ToggleObjectsConfig>
	{
		public void Build(ToggleObjectsConfig config, Hazard hazard, List<IHazardOperation> operations)
		{
			if (config == null)
			{
				Debug.LogError($"ToggleObjectsOperationBuilder: config is null on hazard {hazard?.name}");
				return;
			}

			var op = new ToggleObjectsOperation(
				config.Enables,
				config.Disables
			);

			operations.Add(op);
		}
	}

	public class EventOperationBuilder : IHazardOperationBuilder<EventConfig>
	{
		public void Build(EventConfig config, Hazard hazard, List<IHazardOperation> operations)
		{
			if (config == null)
			{
				Debug.LogError($"EventOperationBuilder: config is null on hazard {hazard?.name}");
				return;
			}

			var op = new EventOperation(config.evt);	
			operations.Add(op);
		}
	}

	[Serializable]
	public class ToggleObjectsConfig 
	{
		public GameObject[] Enables;
		public GameObject[] Disables;
	}

	[Serializable]
	public class EventConfig	
	{
		public UnityEvent evt;
	}

	public interface IHazardOperationBuilder<TConfig>
	{
		void Build(TConfig config, Hazard hazard, List<IHazardOperation> operations);
	}
}
