using UnityEngine;

namespace HazardSystem
{
	public abstract class RayProviderBase : MonoBehaviour
	{
		public abstract bool TryGetRay(out Ray ray);
	}
}
