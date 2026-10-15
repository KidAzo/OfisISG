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

        static bool _enterLogged;

        static void TraceEnter()
        {
            if (_enterLogged)
                return;

            _enterLogged = true;
            Debug.Log("[FireTrace] OfficeFireGameplayCameraSetup ENTER");
        }

        public static void RequestEnsureReady(MonoBehaviour host, string reason)
        {
            TraceEnter();
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
            TraceEnter();
            if (FirePlatformRuntime.IsVR)
            {
                if (OfficeFireStandaloneSceneGate.IsWaitingForAdditiveScenes)
                    return;

                ApplyVrRig(reason);
                DisableLoadingCameraWhenGameplayCameraExists();
                RequestLoadingScreenRefresh();
                return;
            }

            ActivatePcPlayerRootIfNeeded();
            DisableLoadingCameraWhenGameplayCameraExists();
            RequestLoadingScreenRefresh();

            Debug.Log($"[OfficeFireGameplayCameraSetup] Camera setup applied ({reason}).");
        }

        /// <summary>
        /// Fire VR only. Activates the XR separator and turns off the fire PC-Player.
        /// The tracked head camera is not moved here.
        /// </summary>
        static void ApplyVrRig(string reason)
        {
            GameObject xrRoot = FindNamedInLoadedScenes("-------------XR------------");
            if (xrRoot != null && !xrRoot.activeSelf)
                xrRoot.SetActive(true);

            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            bool pcRootActive = false;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
                    continue;
                if (!string.Equals(candidate.name, "PC-Player", System.StringComparison.Ordinal))
                    continue;
                if (IsUnderWasteCollection(candidate) || IsUnderTransform(candidate.transform, xrRoot))
                    continue;

                if (candidate.activeSelf)
                    candidate.SetActive(false);
                if (candidate.activeInHierarchy)
                    pcRootActive = true;
            }

            HidePcOnlyGameplayUi();

            Camera xrCamera = xrRoot != null ? xrRoot.GetComponentInChildren<Camera>(true) : null;
            if (xrCamera != null)
            {
                if (!xrCamera.gameObject.activeSelf)
                    xrCamera.gameObject.SetActive(true);
                xrCamera.enabled = true;
            }

            int rendering = CountRenderingCameras();
            string state =
                "[FirePlatformState]\n" +
                "platform=VR\n" +
                "pcRootActive=" + pcRootActive.ToString().ToLowerInvariant() + "\n" +
                "pcCameraEnabled=" + PcCameraRendering().ToString().ToLowerInvariant() + "\n" +
                "xrRootActive=" + (xrRoot != null && xrRoot.activeInHierarchy).ToString().ToLowerInvariant() + "\n" +
                "xrCameraEnabled=" + (xrCamera != null && xrCamera.enabled && xrCamera.gameObject.activeInHierarchy).ToString().ToLowerInvariant() + "\n" +
                "activeRenderingCameras=" + rendering + "\n" +
                "reason=" + reason;
            if (state != _lastVrState)
            {
                _lastVrState = state;
                Debug.Log(state);
            }
        }

        /// <summary>
        /// Screen-space PC gameplay widgets live under the fire UI root, not under PC-Player,
        /// so turning off the PC rig does not hide them. VR world-space UI is left alone.
        /// </summary>
        static void HidePcOnlyGameplayUi()
        {
            GameObject uiRoot = FindNamedInLoadedScenes("05_UI");
            if (uiRoot != null)
            {
                Transform cross = uiRoot.transform.Find("Cross");
                if (cross != null && cross.gameObject.activeSelf)
                    cross.gameObject.SetActive(false);
            }

            GameObject controlsHud = FindNamedInLoadedScenes("OfficeFireControlsHUD");
            if (controlsHud != null && controlsHud.activeSelf)
                controlsHud.SetActive(false);

            GameObject extinguisherHud = FindNamedInLoadedScenes("EstinguisherHUD");
            if (extinguisherHud != null)
            {
                extinguisherHud.SendMessage(
                    "SetPresentationVisible",
                    false,
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        static string _lastVrState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayModeState()
        {
            _lastVrState = null;
            _enterLogged = false;
        }

        static GameObject FindNamedInLoadedScenes(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || candidate.name != objectName)
                    continue;
                if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                    continue;
                return candidate;
            }

            return null;
        }

        static bool IsUnderTransform(Transform transform, GameObject root)
        {
            if (transform == null || root == null)
                return false;
            return transform == root.transform || transform.IsChildOf(root.transform);
        }

        static bool PcCameraRendering()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.gameObject.name != "PlayerCamera")
                    continue;
                if (IsUnderWasteCollection(camera.gameObject))
                    continue;
                if (camera.enabled && camera.gameObject.activeInHierarchy && camera.targetTexture == null && camera.cullingMask != 0)
                    return true;
            }

            return false;
        }

        static int CountRenderingCameras()
        {
            int count = 0;
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                    continue;
                if (camera.enabled && camera.gameObject.activeInHierarchy && camera.targetTexture == null && camera.cullingMask != 0)
                    count++;
            }

            return count;
        }

        private static void ActivatePcPlayerRootIfNeeded()
        {
            if (FirePlatformRuntime.IsVR)
                return;

            GameObject xrRoot = FindNamedInLoadedScenes("-------------XR------------");
            if (xrRoot != null && xrRoot.activeSelf)
                xrRoot.SetActive(false);

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
