using FireExtinguisher.Core;
using UnityEngine;
using Woi.InputSystem;

namespace FireExtinguisher.PC
{
    /// <summary>
    /// PC implementation of <see cref="ISprayInputProvider"/> using the live
    /// <see cref="GameplayInputContext"/> from <see cref="InputManager"/> (Gameplay/Fire action).
    /// </summary>
    [AddComponentMenu("Fire Extinguisher/PC/PC Spray Input Provider")]
    public sealed class PCSprayInputProvider : MonoBehaviour, ISprayInputProvider, ISoapGameplayInputContextListener
    {
        [SerializeField]
        private GameplayInputContext inputContext;

        private void OnEnable()
        {
            TryBindLiveInputContext();
        }

        private void Start()
        {
            TryBindLiveInputContext();
        }

        public bool IsUsingDifferentGameplayInputContext(GameplayInputContext liveContext) =>
            inputContext != null
            && liveContext != null
            && !ReferenceEquals(inputContext, liveContext);

        public void RebindGameplayInputContext(GameplayInputContext liveContext)
        {
            if (liveContext != null)
                inputContext = liveContext;
        }

        private void TryBindLiveInputContext()
        {
            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            GameplayInputContext liveContext = inputManager?.GetPcGameplayContext();
            if (liveContext == null)
                return;

            if (inputContext == null || IsUsingDifferentGameplayInputContext(liveContext))
                RebindGameplayInputContext(liveContext);
        }

        private IFireInputReader ResolveFireInputReader()
        {
            if (inputContext != null && inputContext.HasInitializedInputActions)
                return inputContext;

            InputManager inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            GameplayInputContext liveContext = inputManager?.GetPcGameplayContext();
            if (liveContext != null)
                return liveContext;

            return inputContext;
        }

        public bool IsSprayHeld => ResolveFireInputReader()?.IsFireHolding ?? false;

        public bool IsSprayStartedThisFrame => ResolveFireInputReader()?.IsFireStartedThisFrame ?? false;

        public bool IsSprayStoppedThisFrame => ResolveFireInputReader()?.IsFireStoppedThisFrame ?? false;
    }
}
