using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Obvious.Soap;

namespace Woi.Settings
{
    public class LoadingScreenController : MonoBehaviour
    {
        private static readonly int[] DisplayFallbackRefreshDelays = { 0, 1, 2, 5, 15, 30, 60, 120 };

        [SerializeField] LoadingScreenSettings[] settings;
        [SerializeField] ScriptableEnumPortingVariable portingVariable;
        LoadingScreenSettings currentLoadingScreenSettings;
        public LoadingScreenSettings CurrentLoadingScreenSettings => currentLoadingScreenSettings;
        Coroutine _displayFallbackRefreshRoutine;

        void Awake()
        {
            if (portingVariable == null)
            {
                Debug.LogError(
                    "[LoadingScreenController] portingVariable is not assigned — cannot select PC/XR loading UI. " +
                    "Assign Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset (same as InputManager). " +
                    "In builds using Addressables, rebuild bundles after changing SceneLoader prefab.");
                HideAllLoadingUi();
                return;
            }

            FirePlatformRuntime.TryInitialize(portingVariable);
            SetLoadingScreen(portingVariable.CurrentValue);
            HideAllLoadingUi();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RequestDelayedDisplayFallbackRefresh();
        }

        public void RequestDelayedDisplayFallbackRefresh()
        {
            if (_displayFallbackRefreshRoutine != null)
            {
                StopCoroutine(_displayFallbackRefreshRoutine);
            }

            _displayFallbackRefreshRoutine = StartCoroutine(DelayedDisplayFallbackRefreshRoutine());
        }

        /// <summary>
        /// Hides every configured loading canvas/camera. VR loading UI is inactive in the prefab by default,
        /// but older prefabs had it enabled — leaving it on blocks UI Toolkit / uGUI input after scene load.
        /// </summary>
        public void HideAllLoadingUi()
        {
            if (settings == null)
                return;

            for (int i = 0; i < settings.Length; i++)
            {
                LoadingScreenSettings entry = settings[i];
                if (entry == null || entry.loadingCanvas == null)
                    continue;

                entry.loadingCanvas.gameObject.SetActive(false);
            }

            RefreshDisplayFallbackCamera();
        }

        /// <summary>
        /// Keeps the SceneLoader loading camera enabled when no other camera is active (e.g. WasteLogin UI-only scene).
        /// Disables it once a gameplay camera exists (e.g. PC-Player after FireModule_Office load).
        /// </summary>
        public void RefreshDisplayFallbackCamera()
        {
            Camera fallbackCamera = GetSharedLoadingCamera();
            if (fallbackCamera == null)
                return;

            if (HasAnotherEnabledCamera(fallbackCamera))
            {
                if (fallbackCamera.gameObject.activeSelf)
                    fallbackCamera.gameObject.SetActive(false);
                return;
            }

            fallbackCamera.gameObject.SetActive(true);
            fallbackCamera.enabled = true;
            fallbackCamera.depth = -100f;
            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = Color.black;
            fallbackCamera.cullingMask = 0;
        }

        private Camera GetSharedLoadingCamera()
        {
            if (settings == null)
                return null;

            for (int i = 0; i < settings.Length; i++)
            {
                LoadingScreenSettings entry = settings[i];
                if (entry?.loadingCamera != null)
                    return entry.loadingCamera;
            }

            return null;
        }

        private static bool HasAnotherEnabledCamera(Camera exclude)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera == exclude)
                    continue;

                if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.cullingMask == 0)
                    continue;

                return true;
            }

            return false;
        }

        private IEnumerator DelayedDisplayFallbackRefreshRoutine()
        {
            int previousDelay = 0;
            for (int i = 0; i < DisplayFallbackRefreshDelays.Length; i++)
            {
                int extraFrames = DisplayFallbackRefreshDelays[i] - previousDelay;
                for (int frame = 0; frame < extraFrames; frame++)
                {
                    yield return null;
                }

                previousDelay = DisplayFallbackRefreshDelays[i];
                RefreshDisplayFallbackCamera();
            }

            _displayFallbackRefreshRoutine = null;
        }

        void SetLoadingScreen(AppMode mode)
        {
            currentLoadingScreenSettings = Array.Find(settings, s => s.mode == mode);
            if (currentLoadingScreenSettings != null)
                return;

            if (mode == AppMode.XR)
                currentLoadingScreenSettings = Array.Find(settings, s => s.mode == AppMode.PC);

            if (currentLoadingScreenSettings == null && settings != null && settings.Length > 0)
                currentLoadingScreenSettings = settings[0];

            if (currentLoadingScreenSettings == null)
            {
                Debug.LogError(
                    "[LoadingScreenController] No LoadingScreenSettings entry found. Assign the settings array with at least one row (canvas, camera, progress bar).");
                return;
            }

            if (mode == AppMode.XR)
                Debug.LogWarning(
                    "[LoadingScreenController] No LoadingScreenSettings for AppMode.XR — using fallback entry (" +
                    currentLoadingScreenSettings.mode + "). Add an XR row or duplicate the PC row with mode XR.");
        }

        [Serializable]
        public class LoadingScreenSettings
        {
            public AppMode mode;
            public Canvas loadingCanvas;
            public Camera loadingCamera;
            public Image progressBar;
            public float fillSpeed;

            [Header("VR (XR) — fullscreen black fade")]
            [Tooltip("Stretched black panel with CanvasGroup (alpha 0 at rest). Used only when mode is XR.")]
            public CanvasGroup xrBlackFadeOverlay;

            [Tooltip("Seconds to fade alpha 0 → 1 after loading UI is shown.")]
            public float xrFadeInDuration = 0.35f;

            [Tooltip("Seconds to fade alpha 1 → 0 before loading UI is hidden.")]
            public float xrFadeOutDuration = 0.35f;

            [Header("VR (XR) — rig during load")]
            [Tooltip("Optional XR Origin / player root. If empty, SceneLoader searches loaded scenes for an XROrigin. Disabled during load, re-enabled before fade-out.")]
            public GameObject xrRigRoot;
        }
    }
}





