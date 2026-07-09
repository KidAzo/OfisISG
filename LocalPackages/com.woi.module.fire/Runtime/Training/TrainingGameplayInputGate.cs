using UnityEngine;
using Woi.Events.Data;
using Woi.InputSystem;
using Woi.Player;
using WOI.Modules.SDK;

namespace Woi.Game.Training
{
    /// <summary>
    /// Blocks gameplay input while the training results UI is shown so spray / interact cannot restart audio.
    /// </summary>
    public static class TrainingGameplayInputGate
    {
        public static bool IsBlocked => TrainingGameplayBlockState.IsBlocked;

        /// <summary>Call when a gameplay/training scene starts so stale block state from a prior session cannot linger.</summary>
        public static void ResetForSceneEntry()
        {
            SetBlocked(false);
        }

        public static void SetBlocked(bool blocked)
        {
            TrainingGameplayBlockState.SetBlocked(blocked);

            GameplayInputContext ctx = ResolveGameplayInputContext();
            PlayerController player = ResolvePlayerController();

            if (blocked)
            {
                ctx?.DisableAllInputs();
                player?.SuppressLocomotionInput();
                TrainingGameplayAudioSilencer.StopAllSceneGameplayAudio();
                return;
            }

            ctx?.EnableAllInputs();
        }

        static GameplayInputContext ResolveGameplayInputContext()
        {
            InputManager inputManager = Object.FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            GameplayInputContext live = inputManager?.GetPcGameplayContext();
            if (live != null)
                return live;

            GameplayInputContext[] contexts = Resources.FindObjectsOfTypeAll<GameplayInputContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                GameplayInputContext ctx = contexts[i];
                if (ctx != null && ctx.HasInitializedInputActions)
                    return ctx;
            }

            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] != null)
                    return contexts[i];
            }

            return null;
        }

        static PlayerController ResolvePlayerController()
        {
            if (ServiceLocator.TryGet<IPlayerService>(out IPlayerService playerService) &&
                playerService != null)
            {
                Transform t = playerService.GetPlayerTransform();
                if (t != null && t.TryGetComponent(out PlayerController onPlayer))
                    return onPlayer;
            }

            return Object.FindFirstObjectByType<PlayerController>();
        }
    }
}
