using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Systems.SceneManagement
{
    /// <summary>
    /// Lightweight pre-load verification so gameplay transitions fail loudly instead of freezing when the catalog
    /// does not contain expected keys (common after a player update without clearing bundle cache).
    /// </summary>
    public static class AddressablesCatalogHealth
    {
        /// <summary>
        /// Resolves each key via Addressables; logs errors if nothing is registered for that key.
        /// </summary>
        public static async UniTask<bool> RunPreGameplayChecksAsync(
            string sceneGroupName,
            IReadOnlyList<string> requiredKeys,
            float timeoutSecondsPerKey = 15f)
        {
            if (requiredKeys == null || requiredKeys.Count == 0)
            {
                Debug.Log($"[ADDR HEALTH] No keys configured — skipping checks for group '{sceneGroupName}'.");
                return true;
            }

            Debug.Log($"[ADDR HEALTH] Pre-gameplay catalog checks for scene group '{sceneGroupName}' ({requiredKeys.Count} key(s)).");

            bool allOk = true;
            foreach (string key in requiredKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(key);
                float deadline = Time.realtimeSinceStartup + timeoutSecondsPerKey;
                while (!handle.IsDone && Time.realtimeSinceStartup < deadline)
                    await UniTask.Yield(PlayerLoopTiming.Update);

                if (!handle.IsDone)
                {
                    allOk = false;
                    Debug.LogError($"[ADDR HEALTH] TIMEOUT resolving key '{key}' (catalog/network stall).");
                    Addressables.Release(handle);
                    continue;
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    allOk = false;
                    Debug.LogError($"[ADDR HEALTH] FAIL key '{key}' status={handle.Status} err={handle.OperationException}");
                    Addressables.Release(handle);
                    continue;
                }

                if (handle.Result == null || handle.Result.Count == 0)
                {
                    allOk = false;
                    Debug.LogError(
                        $"[ADDR HEALTH] Catalog has NO locations for key '{key}'. Rebuild Addressables or clear cache — internal ids likely out of sync.");
                }
                else
                {
                    Debug.Log($"[ADDR HEALTH] OK key '{key}' → {handle.Result.Count} location(s).");
                }

                Addressables.Release(handle);
            }

            return allOk;
        }
    }
}
