using System.Collections;
using UnityEngine;
using Woi.Settings;

namespace Woi.OfficeFire
{    /// <summary>
    /// Ensures PC gameplay camera is active and the SceneLoader fallback loading camera is disabled.
    /// Hub loads FireModule_Office via Addressables after login; the loading camera can stay
    /// on top (solid black) until PC-Player is enabled.
    /// </summary>
    public static class OfficeFireGameplayCameraSetup
    {
        private const string LoadingCameraObjectName = "LoadingCameraPc";
        private static readonly int[] RetryFrameDelays = { 0, 1, 2, 5, 15, 30, 60 };

        public static void RequestEnsureReady(MonoBehaviour host, string reason)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                Apply(reason);
                return;
            }

            host.StartCoroutine(EnsureReadyRoutine(reason));
        }

        private static IEnumerator EnsureReadyRoutine(string reason)
        {
            int previousDelay = 0;
            for (int i = 0; i < RetryFrameDelays.Length; i++)
            {
                int extraFrames = RetryFrameDelays[i] - previousDelay;
                for (int frame = 0; frame < extraFrames; frame++)
                {
                    yield return null;
                }

                previousDelay = RetryFrameDelays[i];
                Apply($"{reason} @pass {i}");
            }
        }

        private static void Apply(string reason)
        {
            ActivatePcPlayerRootIfNeeded();
            DisableLoadingCameraWhenGameplayCameraExists();
            RequestLoadingScreenRefresh();

            Debug.Log($"[OfficeFireGameplayCameraSetup] Camera setup applied ({reason}).");
        }

        private static void ActivatePcPlayerRootIfNeeded()
        {
            if (FirePlatformRuntime.IsVR)
                return;

            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            GameObject selected = null;

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject candidate = allObjects[i];
                if (candidate == null || !candidate.scene.IsValid() || candidate.activeSelf)
                {
                    continue;
                }

                if (!string.Equals(candidate.name, "PC-Player", System.StringComparison.Ordinal)
                    && !candidate.CompareTag("Player"))
                {
                    continue;
                }

                if (IsUnderWasteCollection(candidate))
                {
                    continue;
                }

                selected = candidate;
                break;
            }

            if (selected == null)
            {
                for (int i = 0; i < allObjects.Length; i++)
                {
                    GameObject candidate = allObjects[i];
                    if (candidate == null || !candidate.scene.IsValid() || candidate.activeSelf)
                    {
                        continue;
                    }

                    if (candidate.CompareTag("Player"))
                    {
                        selected = candidate;
                        break;
                    }
                }
            }

            if (selected == null)
            {
                return;
            }

            selected.SetActive(true);
            Debug.Log($"[OfficeFireGameplayCameraSetup] Activated inactive Player root '{selected.name}'.");
        }

        private static bool IsUnderWasteCollection(GameObject root)
        {
            Transform transform = root.transform;
            while (transform != null)
            {
                if (transform.name.IndexOf("WasteCollection", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static void DisableLoadingCameraWhenGameplayCameraExists()
        {
            if (!HasGameplayCamera(exclude: null))
            {
                return;
            }

            GameObject loadingCameraObject = GameObject.Find(LoadingCameraObjectName);
            if (loadingCameraObject == null || !loadingCameraObject.activeSelf)
            {
                return;
            }

            loadingCameraObject.SetActive(false);
            Debug.Log("[OfficeFireGameplayCameraSetup] Disabled LoadingCameraPc.");
        }

        private static bool HasGameplayCamera(Camera exclude)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera == exclude)
                {
                    continue;
                }

                if (string.Equals(camera.gameObject.name, LoadingCameraObjectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.cullingMask == 0)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void RequestLoadingScreenRefresh()
        {
            LoadingScreenController[] controllers = Object.FindObjectsByType<LoadingScreenController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i]?.RequestDelayedDisplayFallbackRefresh();
            }
        }
    }
}
