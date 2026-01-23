using System.Collections.Generic;
using UnityEngine;
using Woi.Events;

namespace HazardSystem
{
	public abstract class Hazard : MonoBehaviour, IHazard
	{
		public HazardData data;

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

			EventBus.Publish(new OnHazardFixed(data.hazardName, data.description, data.score));
			//HazardFixedSoundController.PlaySound(data.soundData);
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
		ToggleObjects,   // Objeleri a�/kapat
		Transform,       // Pozisyon/rotation d�zelt
		Animation,        // Animasyon de�i�tir (�rnek)
	    Event
	}
}