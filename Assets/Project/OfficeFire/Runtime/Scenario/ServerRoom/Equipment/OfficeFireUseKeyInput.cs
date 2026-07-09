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
        private static InputManager _cachedInputManager;
        private static InputAction _cachedDropAction;
        private static bool _dropEnabledConfigured;
        private static bool _sceneLookupAttempted;

        public static bool WasUseKeyPressedThisFrame(Key fallbackKey = Key.G)
        {
            if (TryReadDropFromInputManager(out bool pressed))
            {
                return pressed;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[fallbackKey].wasPressedThisFrame;
        }

        private static bool TryReadDropFromInputManager(out bool pressed)
        {
            pressed = false;

            InputAction dropAction = ResolveDropAction();
            if (dropAction == null)
            {
                return false;
            }

            pressed = dropAction.WasPressedThisFrame();
            return true;
        }

        private static InputAction ResolveDropAction()
        {
            if (_cachedDropAction != null)
            {
                return _cachedDropAction;
            }

            InputManager inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                return null;
            }

            if (!_dropEnabledConfigured)
            {
                inputManager.EnsurePcGameplayInputEnabled();
                GameplayInputContext gameplayContext = inputManager.GetPcGameplayContext();
                gameplayContext?.SetDropEnabled(true);
                _dropEnabledConfigured = true;
            }

            PlayerInputActions actions = inputManager.InputActions;
            if (actions == null)
            {
                return null;
            }

            PlayerInputActions.GameplayActions gameplay = actions.Gameplay;
            if (!gameplay.enabled)
            {
                gameplay.Enable();
            }

            _cachedDropAction = gameplay.Drop;
            return _cachedDropAction;
        }

        private static InputManager ResolveInputManager()
        {
            if (_cachedInputManager != null)
            {
                return _cachedInputManager;
            }

            if (ServiceLocator.TryGet<IInputProvider>(out IInputProvider provider)
                && provider is InputManager serviceManager)
            {
                _cachedInputManager = serviceManager;
                return _cachedInputManager;
            }

            if (ServiceLocator.TryGet(out InputManager registeredManager) && registeredManager != null)
            {
                _cachedInputManager = registeredManager;
                return _cachedInputManager;
            }

            // Expensive scene scan — at most once for the process lifetime.
            if (!_sceneLookupAttempted)
            {
                _sceneLookupAttempted = true;
                _cachedInputManager = UnityEngine.Object.FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            }

            return _cachedInputManager;
        }
    }
}
