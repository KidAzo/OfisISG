using UnityEngine;
using UnityEngine.InputSystem;
using Woi.InputSystem;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Reads the office "use/drop" key (G) through the live gameplay input map when available.
    /// Raw <see cref="Keyboard.current"/> misses presses when the Gameplay action map owns G.
    /// </summary>
    internal static class OfficeFireUseKeyInput
    {
        public static bool WasUseKeyPressedThisFrame(Key fallbackKey = Key.G)
        {
            if (TryReadDropFromInputManager(out bool pressed) && pressed)
            {
                return true;
            }

            return Keyboard.current != null && Keyboard.current[fallbackKey].wasPressedThisFrame;
        }

        private static bool TryReadDropFromInputManager(out bool pressed)
        {
            pressed = false;
            InputManager inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                return false;
            }

            inputManager.EnsurePcGameplayInputEnabled();
            GameplayInputContext gameplayContext = inputManager.GetPcGameplayContext();
            gameplayContext?.SetDropEnabled(true);

            PlayerInputActions actions = inputManager.InputActions;
            if (actions == null)
            {
                return false;
            }

            var gameplay = actions.Gameplay;
            if (!gameplay.enabled)
            {
                gameplay.Enable();
            }

            pressed = gameplay.Drop.WasPressedThisFrame();
            return true;
        }

        private static InputManager ResolveInputManager()
        {
            if (ServiceLocator.TryGet<IInputProvider>(out IInputProvider provider)
                && provider is InputManager serviceManager)
            {
                return serviceManager;
            }

            if (ServiceLocator.TryGet(out InputManager registeredManager) && registeredManager != null)
            {
                return registeredManager;
            }

            return UnityEngine.Object.FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
        }
    }
}