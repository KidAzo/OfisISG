using UnityEngine;

namespace Woi.HazardSystem
{
	public abstract class RayProviderBase : MonoBehaviour
	{
		public abstract bool TryGetRay(out Ray ray);
	}
}
