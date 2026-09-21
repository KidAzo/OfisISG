// using System;
// using Zenject;
// using UnityEngine;

// namespace HazardSystem
// {
// 	public class VrInteractionInput : InteractionInputBase
// 	{
// 		public override event Action OnInteractPressed;
// 		[Inject] XRInputManager inputManager;

// 		private void OnEnable()
// 		{
// 			inputManager.RegisterCallback(XRInputManager.InputType.HazardFix, TryFixCurrentHazard);
// 			inputManager.EnableActionMap(XRInputManager.ActionMapType.RightHand);
// 		}

// 		private void OnDisable()
// 		{
// 			inputManager.UnregisterCallback(XRInputManager.InputType.HazardFix, TryFixCurrentHazard);
// 			inputManager.DisableActionMap(XRInputManager.ActionMapType.RightHand);	
// 		}

// 		void TryFixCurrentHazard()
//         {
//             OnInteractPressed?.Invoke();
//         }
// 	}
// }
