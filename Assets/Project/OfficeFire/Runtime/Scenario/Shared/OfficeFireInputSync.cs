using System.Collections;
using UnityEngine;
using Woi.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Re-syncs gameplay input after office scenario roots activate (PC Soap events + VR pin-pull / grab).
    /// Hub + Addressables can load duplicate ScriptableObject instances; listeners that enable
    /// after <see cref="InputManager"/> sceneLoaded sync may miss the live Interact (E) chain.
    /// </summary>
    public static class OfficeFireInputSync
    {
        static readonly int[] RetryFrameDelays = { 0, 1, 2, 5, 15 };

        public static void RequestDelayedSync(MonoBehaviour host, string reason)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                ApplySync(reason);
                return;
            }

            host.StartCoroutine(DelayedSyncRoutine(reason));
        }

        public static void ApplySync(string reason)
        {
            InputManager inputManager = UnityEngine.Object.FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
            if (inputManager == null)
            {
                Debug.LogWarning($"[OfficeFireInputSync] InputManager not found — skip sync ({reason}).");
                return;
            }

            if (FirePlatformRuntime.IsVR)
            {
                inputManager.EnsureVrGameplayInputEnabled();
                inputManager.SyncPcPlayerSoapEvents();
                OfficeFireVrExtinguisherRigBootstrap.EnsureWired();
                Debug.Log($"[OfficeFireInputSync] VR input synced ({reason}).");
                return;
            }

            inputManager.EnsurePcGameplayInputEnabled();
            GameplayInputContext gameplayContext = inputManager.GetPcGameplayContext();
            if (gameplayContext != null)
            {
                gameplayContext.EnableAllInputs();
                gameplayContext.SetDropEnabled(true);
                gameplayContext.SetEquipEnabled(true);
                gameplayContext.SetInteractEnabled(true);
            }

            Debug.Log($"[OfficeFireInputSync] PC input synced ({reason}).");
        }

        static IEnumerator DelayedSyncRoutine(string reason)
        {
            int previousDelay = 0;
            for (int i = 0; i < RetryFrameDelays.Length; i++)
            {
                int extraFrames = RetryFrameDelays[i] - previousDelay;
                for (int f = 0; f < extraFrames; f++)
                {
                    yield return null;
                }

                previousDelay = RetryFrameDelays[i];
                ApplySync($"{reason} @pass {i}");
            }
        }
    }
}
