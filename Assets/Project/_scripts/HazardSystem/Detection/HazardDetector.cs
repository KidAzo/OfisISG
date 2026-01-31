using System;
using System.Linq;
using UnityEngine;

namespace Woi.HazardSystem
{
	public class HazardDetector : MonoBehaviour
	{
		[Header("Providers")]
		[SerializeField] private RayProviderBase rayProvider;
		[SerializeField] private InteractionInputBase inputProvider;

		[Header("Settings")]
		[SerializeField] private float maxDistance = 40f;
		[SerializeField] private LayerMask hazardLayerMask;

		private Hazard _currentHazard;

		private event Action<Hazard> OnHazardDetected;
		private event Action<Hazard> OnHazardUndetected;

		private void OnEnable()
		{
			inputProvider.OnInteractPressed += TryFixCurrentHazard;
		}

		private void OnDisable()
		{
			inputProvider.OnInteractPressed -= TryFixCurrentHazard;
		}

		private void Update()
		{
			UpdateCurrentHazard();
		}

		private void UpdateCurrentHazard()
		{
			if (!rayProvider.TryGetRay(out var ray))
			{
				ClearSelectionIfNeeded();
				return;
			}

			RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, hazardLayerMask);

			Debug.Log($"Raycast Hits: {hits.Length}");

			if (hits.Length > 0)
			{
				var sortedHits = hits.OrderBy(h => h.distance);

				foreach (var hit in sortedHits)
				{
					var hazards = hit.collider.GetComponents<Hazard>();

						Debug.Log($"Hazards Found: {hazards.Length}");

					if (hazards != null && hazards.Length > 0)
					{
						Debug.Log("Processing Hazards");
						var hazard = hazards.FirstOrDefault(h => !h.IsFixed);

						if (hazard != null)
						{
							Debug.Log($"Current Hazard: {hazard.name}");
							if (_currentHazard == hazard)
								return;

							Debug.Log("Setting New Hazard");
							
							ClearCurrentHazardHighlight();
							_currentHazard = hazard;
							SetCurrentHazardHighlight();
							return;
						}
					}
				}
			}

			ClearSelectionIfNeeded();
		}

		private void ClearSelectionIfNeeded()
		{
			if (_currentHazard != null)
			{
				ClearCurrentHazardHighlight();
				_currentHazard = null;
			}
		}

		private void TryFixCurrentHazard()
		{
			if (_currentHazard == null) return;
			if (_currentHazard.IsFixed) return;
			
			Debug.Log("Fixed");
			_currentHazard.Fix();
			ClearCurrentHazardHighlight();
			_currentHazard = null;
		}

		#region Highlight Hooks

		private void SetCurrentHazardHighlight()
		{
			if (_currentHazard == null) return;

			OnHazardDetected?.Invoke(_currentHazard);
		}

		private void ClearCurrentHazardHighlight()
		{
			if (_currentHazard == null) return;

			OnHazardUndetected?.Invoke(_currentHazard);
		}

		#endregion
	}
}
