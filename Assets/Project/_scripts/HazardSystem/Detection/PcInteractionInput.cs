using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Woi.HazardSystem;

namespace Woi.HazardSystem
{
    public class PcInteractionInput : InteractionInputBase
    {
        public override event Action OnInteractPressed;

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                OnInteractPressed?.Invoke();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                OnInteractPressed?.Invoke();
        }
    }
}
