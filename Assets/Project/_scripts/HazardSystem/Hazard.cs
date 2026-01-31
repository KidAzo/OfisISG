using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;

namespace Woi.HazardSystem	
{
	public abstract class Hazard : MonoBehaviour, IHazard, IInteractable
	{
		public HazardData data;
		public ScriptableEventNoParam onHazardFixedEvent;
		public UnityEvent onHazardFixedUE;

		protected readonly List<IHazardOperation> operations = new();

		public bool IsFixed { get; private set; }

		protected virtual void Awake()
		{
			BuildOperations();
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

			EventBus.Publish(new OnHazardFixed(data.TaskName, data.description, data.score, data.soundDefinition));
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
}