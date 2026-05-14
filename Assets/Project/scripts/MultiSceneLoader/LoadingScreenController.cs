using System;
using UnityEngine;
using UnityEngine.UI;
using Obvious.Soap;

namespace Woi.Settings
{
    public class LoadingScreenController : MonoBehaviour
    {
        [SerializeField] LoadingScreenSettings[] settings;
        [SerializeField] ScriptableEnumPortingVariable portingVariable;
        LoadingScreenSettings currentLoadingScreenSettings;
        public LoadingScreenSettings CurrentLoadingScreenSettings => currentLoadingScreenSettings;

        void Awake()
        {
            FirePlatformRuntime.TryInitialize(portingVariable);
            SetLoadingScreen(portingVariable.CurrentValue);
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





