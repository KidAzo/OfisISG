using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Woi.Events;

namespace Woi.HazardSystem	
{
	public class ControllerRayProvider : RayProviderBase
	{
		private XRRayInteractor rayInteractor;

		private void OnEnable()
		{
			EventBus.Subscribe<OnSceneGroupLoaded>(InitRayInteractor);
		}

		private void OnDisable()
		{
			EventBus.Unsubscribe<OnSceneGroupLoaded>(InitRayInteractor);
		}

		private void InitRayInteractor(OnSceneGroupLoaded evt)
		{
			//rayInteractor = XRPlayerView.Instance.rayInteractor;
		}

		public override bool TryGetRay(out Ray ray)
		{
			Debug.Log(rayInteractor);
			
			if (rayInteractor == null || !rayInteractor.enabled)
			{
				ray = default;
				return false;
			}

			Transform attachTransform = rayInteractor.attachTransform;

			if (attachTransform == null)
			{
				// Fallback: Ray Interactor'�n kendi transform'unu kullan
				attachTransform = rayInteractor.transform;
			}

			ray = new Ray(attachTransform.position, attachTransform.forward);
			return true;
		}

		public bool TryGetRaycastHit(out RaycastHit hit)
		{
			if (rayInteractor == null || !rayInteractor.enabled)
			{
				hit = default;
				return false;
			}

			return rayInteractor.TryGetCurrent3DRaycastHit(out hit);
		}
	}
}
