using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;
using WoiUtils;

namespace HazardSystem
{
	public class HazardManager : Singleton<HazardManager>
	{
		[Header("Events")]
		public UnityEvent<HazardData> OnHazardRegistered;
		public UnityEvent<int> OnScoreChanged;
		public UnityEvent<float> OnProgressChanged; 

		public readonly List<Hazard> Hazards = new List<Hazard>();
		private int _currentScore;

		public int CurrentScore => _currentScore;
		public int MaxScore => Hazards.Sum(h => h.data.score);
		public int FixedCount => Hazards.Count(h => h.IsFixed);
		public int TotalCount => Hazards.Count;

		#region Register / Unregister

		private void Start()
		{
			var hazards = FindObjectsByType<Hazard>(
				  FindObjectsInactive.Exclude,  
				  FindObjectsSortMode.None    
			  );

			foreach (var hazard in hazards)
			{
				Register(hazard);
			}
		}

		public void Register(Hazard hazard)
		{
			if (hazard == null) return;
			if (Hazards.Contains(hazard)) return;

			Hazards.Add(hazard);
			OnHazardRegistered?.Invoke(hazard.data);

			if (hazard.IsFixed)
			{
				AddScore(hazard.data.score);
			}

			UpdateProgress();
		}

		public void Unregister(Hazard hazard)
		{
			if (hazard == null) return;
			if (Hazards.Remove(hazard))
			{
				UpdateProgress();
			}
		}

		#endregion

		#region Fixed bildirimi

		private void OnEnable()
		{
			EventBus.Subscribe<OnHazardFixed>(NotifyFixed);
		}

		private void OnDisable()
		{
			EventBus.Unsubscribe<OnHazardFixed>(NotifyFixed);	
		}

		public void NotifyFixed(OnHazardFixed evt)
		{
			Debug.Log($"Hazard Fixed: {evt.hazardTitle}, Score: {evt.score}");	
			AddScore(evt.score);
			UpdateProgress();
		}

		private void AddScore(int add)
		{
			_currentScore += add;
			OnScoreChanged?.Invoke(_currentScore);
		}

		private void UpdateProgress()
		{
			var total = TotalCount;
			if (total <= 0)
			{
				OnProgressChanged?.Invoke(0f);
				return;
			}

			float progress = (float)FixedCount / total;
			OnProgressChanged?.Invoke(progress);
		}

		#endregion

		public void GetHazardResults(
			out List<HazardData> fixedHazards,
			out List<HazardData> unfixedHazards)
		{
			fixedHazards = new List<HazardData>();
			unfixedHazards = new List<HazardData>();

			foreach (var h in Hazards)
			{
				if (h.IsFixed)
					fixedHazards.Add(h.data);
				else
					unfixedHazards.Add(h.data);
			}
		}

		// public HazardCheckResult BuildHazardCheckResult()
		// {
		// 	GetHazardResults(out var fixedHazards, out var unfixedHazards);

		// 	var result = new HazardCheckResult();

		// 	Debug.Log(fixedHazards.Count);
		// 	Debug.Log(unfixedHazards.Count);
			
		// 	foreach (var h in fixedHazards)
		// 	{
		// 		result.foundedChecks.Add(h);
		// 	}

		// 	foreach (var h in unfixedHazards)
		// 	{
		// 		result.missedChecks.Add(h);
		// 	}

		// 	return result;
		// }
	}
}
