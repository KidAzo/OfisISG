using System;
using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;
using Woi.Localization;

namespace Woi.HazardSystem	
{
	public abstract class Hazard : MonoBehaviour, IHazard, IInteractable
	{
		public List<HazardSettings> hazardSettings;
		[HideInInspector] public HazardData currentData;
		public ScriptableEventNoParam onHazardFixedEvent;
		public UnityEvent onHazardFixedUE;

		protected readonly List<IHazardOperation> operations = new();

		public bool IsFixed { get; private set; }

		protected virtual void Awake()
		{
			BuildOperations();
			SetHazardData();
		}

		public void SetHazardData()
		{
			var language = LanguageManager.CurrentLanguage;

			foreach (var setting in hazardSettings)
			{
				if (setting.language == language)
				{
					currentData = setting.hazardData;
					return;
				}
			}

			Debug.LogWarning($"Hazard data not found for language: {language}. Using default.");
			currentData = hazardSettings[0].hazardData;
		}

		/// <summary>
		/// T�reyen hazard tipi, kendi operation�lar�n� burada kuracak.
		/// </summary>
		protected abstract void BuildOperations();

		public void Fix()
		{
			if (IsFixed) return;

			foreach (var op in operations)
				op.Execute();

			IsFixed = true;

			EventBus.Publish(new OnHazardFixed(currentData.TaskName, currentData.description, currentData.score, currentData.soundDefinition));
			onHazardFixedEvent?.Raise();
			onHazardFixedUE?.Invoke();
		}

        public void Interact()
        {
            Fix();
        }
    }

	public interface IHazard
	{
		void Fix();
	}

	public interface IHazardConfigurator
	{
		void Configure(IHazard hazard, List<IHazardOperation> operations);
	}	

	public interface IHazardOperation
	{
		void Execute();
	}

	public enum HazardType
	{
		ToggleObjects = 0,   
		Position = 1,
		Rotation = 2,      
		Animation = 3,      
	    Event = 4
	}

	[Serializable]
	public struct HazardSettings
	{
		public HazardData hazardData;
		public Language language;
	}
}