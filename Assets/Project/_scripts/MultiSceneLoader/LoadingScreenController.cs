using System;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Woi.Porting;

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
            SetLoadingScreen(portingVariable.Value);
        }

        void SetLoadingScreen(AppMode mode)
        {
            currentLoadingScreenSettings = Array.Find(settings, s => s.mode == mode);
        }

        [Serializable]
        public class LoadingScreenSettings
        {
            public AppMode mode;
            public Canvas loadingCanvas;
            public Camera loadingCamera;
            public Image progressBar;
            public float fillSpeed;
        }
    }
}





