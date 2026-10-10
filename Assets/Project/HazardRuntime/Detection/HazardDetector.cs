using System;
using UnityEngine;

namespace Woi.HazardSystem
{
	public class HazardDetector : MonoBehaviour
	{
		const int HitBufferSize = 32;

		[Header("Providers")]
		[SerializeField] private RayProviderBase rayProvider;
		[SerializeField] private InteractionInputBase inputProvider;

		[Header("Settings")]
		[SerializeField] private float maxDistance = 40f;
		[SerializeField] private LayerMask hazardLayerMask;

		readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];
		private Hazard _currentHazard;

		private event Action<Hazard> OnHazardDetected;
		private event Action<Hazard> OnHazardUndetected;

		private void OnEnable()
		{
			SubscribeInput();
		}

		private void Start()
		{
			SubscribeInput();
		}

		private void OnDisable()
		{
			if (inputProvider != null)
				inputProvider.OnInteractPressed -= TryFixCurrentHazard;
		}

		private void SubscribeInput()
		{
			if (inputProvider == null)
				return;

			inputProvider.OnInteractPressed -= TryFixCurrentHazard;
			inputProvider.OnInteractPressed += TryFixCurrentHazard;
		}

		private void Update()
		{
			if (rayProvider == null)
				return;

			UpdateCurrentHazard();
		}

		private void UpdateCurrentHazard()
		{
			if (!rayProvider.TryGetRay(out var ray))
			{
				ClearSelectionIfNeeded();
				return;
			}

			int hitCount = Physics.RaycastNonAlloc(ray, _hits, maxDistance, hazardLayerMask);
			Hazard found = null;
			float bestDistance = float.MaxValue;

			for (int i = 0; i < hitCount; i++)
			{
				var hit = _hits[i];
				if (hit.collider == null)
					continue;

				var hazard = hit.collider.GetComponent<Hazard>();
				if (hazard == null || hazard.IsFixed)
					continue;

				if (hit.distance < bestDistance)
				{
					bestDistance = hit.distance;
					found = hazard;
				}
			}

			if (found == null)
			{
				ClearSelectionIfNeeded();
				return;
			}

			if (_currentHazard == found)
				return;

			ClearCurrentHazardHighlight();
			_currentHazard = found;
			SetCurrentHazardHighlight();
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
