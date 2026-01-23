using UnityEngine;

namespace HazardSystem
{
	public abstract class InteractionInputBase : MonoBehaviour
	{
		public abstract event System.Action OnInteractPressed;
	}
}
