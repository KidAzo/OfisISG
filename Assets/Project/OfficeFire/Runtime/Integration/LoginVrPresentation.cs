using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.OfficeFire
{
    /// <summary>
    /// VR login look: hides the PC background plate (Gauss photo / leaderboard) on the login UIDocument
    /// and clears the XR rig camera to solid black. Everything is restored on disable/destroy.
    /// PC mode only logs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoginVrPresentation : MonoBehaviour
    {
        const int MaxBackgroundSearchFrames = 300;

        static readonly string[] HiddenElementNames = { "LoginBackground", "LeaderboardPanel" };

        // LoadingScreen.uxml carries the PC photo inline on its ".root" element, which also parents the form card.
        const string PhotoRootClass = "root";

        struct CameraState
        {
            public CameraClearFlags ClearFlags;
            public Color BackgroundColor;
        }

        struct ElementState
        {
            public VisualElement Element;
            public StyleEnum<DisplayStyle> Display;
            public StyleBackground BackgroundImage;
            public StyleColor BackgroundColor;
            public bool ClearedPhoto;
        }

        readonly Dictionary<Camera, CameraState> _cameraStates = new();
        readonly List<ElementState> _elementStates = new();

        GameObject _xrOriginRoot;
        UIDocument _uiDocument;
        VisualElement _appliedRoot;
        bool _vr;
        bool _backgroundHidden;
        bool _logged;
        int _searchFrames;

        public void Initialize(GameObject xrOriginRoot, bool vr)
        {
            _xrOriginRoot = xrOriginRoot;
            _vr = vr;
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (!_vr)
            {
                LogPc();
                return;
            }

            ApplyCameras();
            TryHideBackground();
        }

        void LateUpdate()
        {
            if (!_vr)
                return;

            ApplyCameras();

            VisualElement root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (root != _appliedRoot)
            {
                RestoreElements();
                _backgroundHidden = false;
                _searchFrames = 0;
            }

            if (!_backgroundHidden && _searchFrames < MaxBackgroundSearchFrames)
            {
                _searchFrames++;
                TryHideBackground();
                if (!_backgroundHidden && _searchFrames >= MaxBackgroundSearchFrames)
                    LogVr();
            }
        }

        void OnDisable()
        {
            RestoreElements();
            RestoreCameras();
            _backgroundHidden = false;
            _appliedRoot = null;
        }

        void ApplyCameras()
        {
            if (_xrOriginRoot == null)
                return;

            Camera[] cameras = _xrOriginRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                    continue;

                if (!_cameraStates.ContainsKey(camera))
                {
                    _cameraStates[camera] = new CameraState
                    {
                        ClearFlags = camera.clearFlags,
                        BackgroundColor = camera.backgroundColor,
                    };
                }

                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        void RestoreCameras()
        {
            foreach (KeyValuePair<Camera, CameraState> pair in _cameraStates)
            {
                if (pair.Key == null)
                    continue;

                pair.Key.clearFlags = pair.Value.ClearFlags;
                pair.Key.backgroundColor = pair.Value.BackgroundColor;
            }

            _cameraStates.Clear();
        }

        void TryHideBackground()
        {
            VisualElement root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (root == null || root.childCount == 0)
                return;

            _appliedRoot = root;

            for (int i = 0; i < HiddenElementNames.Length; i++)
            {
                VisualElement element = root.Q(HiddenElementNames[i]);
                if (element == null || IsTracked(element))
                    continue;

                _elementStates.Add(new ElementState
                {
                    Element = element,
                    Display = element.style.display,
                });
                element.style.display = DisplayStyle.None;
            }

            VisualElement photoRoot = root.Q(className: PhotoRootClass);
            if (photoRoot != null && !IsTracked(photoRoot))
            {
                _elementStates.Add(new ElementState
                {
                    Element = photoRoot,
                    BackgroundImage = photoRoot.style.backgroundImage,
                    BackgroundColor = photoRoot.style.backgroundColor,
                    ClearedPhoto = true,
                });
                photoRoot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                photoRoot.style.backgroundColor = Color.clear;
            }

            if (_elementStates.Count == 0)
                return;

            _backgroundHidden = true;
            LogVr();
        }

        bool IsTracked(VisualElement element)
        {
            for (int i = 0; i < _elementStates.Count; i++)
            {
                if (_elementStates[i].Element == element)
                    return true;
            }

            return false;
        }

        void RestoreElements()
        {
            for (int i = 0; i < _elementStates.Count; i++)
            {
                ElementState state = _elementStates[i];
                if (state.Element == null)
                    continue;

                if (state.ClearedPhoto)
                {
                    state.Element.style.backgroundImage = state.BackgroundImage;
                    state.Element.style.backgroundColor = state.BackgroundColor;
                }
                else
                {
                    state.Element.style.display = state.Display;
                }
            }

            _elementStates.Clear();
        }

        void LogVr()
        {
            if (_logged)
                return;

            _logged = true;
            Camera camera = FirstTrackedCamera();
            Log("VR", _backgroundHidden, camera);
        }

        void LogPc()
        {
            if (_logged)
                return;

            _logged = true;
            Log("PC", false, Camera.main);
        }

        void Log(string mode, bool backgroundHidden, Camera camera)
        {
            Debug.Log(
                "[LoginVrPresentation] mode=" + mode
                + " backgroundHidden=" + (backgroundHidden ? "true" : "false")
                + " clearFlags=" + (camera != null ? camera.clearFlags.ToString() : "n/a")
                + " backgroundColor=" + (camera != null ? camera.backgroundColor.ToString() : "n/a")
                + " camera=" + (camera != null ? camera.name : "none")
                + " scene=" + gameObject.scene.name,
                this);
        }

        Camera FirstTrackedCamera()
        {
            foreach (KeyValuePair<Camera, CameraState> pair in _cameraStates)
            {
                if (pair.Key != null)
                    return pair.Key;
            }

            return null;
        }
    }
}
