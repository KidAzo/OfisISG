using UnityEngine;

namespace Woi.HazardSystem
{
	public abstract class InteractionInputBase : MonoBehaviour
	{
		public abstract event System.Action OnInteractPressed;
	}
}
