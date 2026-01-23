using UnityEngine;

namespace HazardSystem
{
	public class CameraCenterRayProvider : RayProviderBase
	{
		[SerializeField] private Camera cam;

		private void Start()
		{
			//cam = PlayerView.Instance.GetComponentInChildren<Camera>();
		}

		public override bool TryGetRay(out Ray ray)
		{
			ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f)); // middle of the screen
			return true;
		}
	}
}
