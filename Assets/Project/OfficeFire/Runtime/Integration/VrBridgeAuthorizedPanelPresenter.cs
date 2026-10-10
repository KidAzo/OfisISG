using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Woi.OfficeFire.VrBridge
{
    /// <summary>
    /// Presentation for the Bridge prepared-training panel. Quest cannot show a screen-space overlay, so in VR the
    /// panel's own canvas becomes a world-space canvas in front of the head, clicked through XRUIInputModule like the
    /// Office and Waste world UIs. PC keeps the overlay. Authorization state and the Start call stay in
    /// AuthorizedTrainingPanel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class VrBridgeAuthorizedPanelPresenter : MonoBehaviour
    {
        const float DistanceMeters = 1.4f;
        const float VerticalOffsetMeters = -0.15f;
        const float MetersPerPixel = 0.0015f;
        const int HeadSearchFrames = 120;

        [SerializeField] bool _forceWorldSpace;

        Transform _worldParent;
        Coroutine _placementRoutine;
        static bool _warnedNoEventSystem;

        public bool IsWorldSpace { get; private set; }

        public void Initialize(Transform worldParent, bool forceWorldSpace = false)
        {
            _worldParent = worldParent;
            _forceWorldSpace = forceWorldSpace;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Present();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_placementRoutine != null)
            {
                StopCoroutine(_placementRoutine);
                _placementRoutine = null;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The head camera and the EventSystem belong to the scene, so both change with it.
            Present();
        }

        public void Present()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            // A nested canvas only receives raycasts through its own raycaster.
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (!_forceWorldSpace && !IsVrLikely())
            {
                return;
            }

            ConvertToWorldSpace(canvas);
            EnsureXrUiInput();

            if (_placementRoutine != null)
            {
                StopCoroutine(_placementRoutine);
            }

            _placementRoutine = StartCoroutine(PlaceWhenHeadAvailable(canvas));
        }

        void ConvertToWorldSpace(Canvas canvas)
        {
            if (!IsWorldSpace)
            {
                var parent = _worldParent != null ? _worldParent : transform.root;
                if (transform.parent != parent)
                {
                    transform.SetParent(parent, false);
                }

                canvas.renderMode = RenderMode.WorldSpace;
                transform.localScale = Vector3.one * MetersPerPixel;
                IsWorldSpace = true;
            }

            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }

        IEnumerator PlaceWhenHeadAvailable(Canvas canvas)
        {
            for (int frame = 0; frame < HeadSearchFrames; frame++)
            {
                var head = ResolveHead();
                if (head != null)
                {
                    PlaceInFront(canvas, head);
                    _placementRoutine = null;
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[VRBridge] No head camera found for the prepared-training panel; it stays at its last position.");
            _placementRoutine = null;
        }

        void PlaceInFront(Canvas canvas, Camera head)
        {
            var headTransform = head.transform;
            var forward = Vector3.ProjectOnPlane(headTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(headTransform.up, Vector3.up);
            }

            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            transform.position = headTransform.position + forward * DistanceMeters + Vector3.up * VerticalOffsetMeters;
            // A world-space canvas reads correctly when its forward points away from the viewer.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            canvas.worldCamera = head;
            EnsureXrUiInput();
        }

        static Camera ResolveHead()
        {
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled)
            {
                return main;
            }

            foreach (var camera in Camera.allCameras)
            {
                if (camera != null && camera.isActiveAndEnabled && camera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    return camera;
                }
            }

            return null;
        }

        static void EnsureXrUiInput()
        {
            var eventSystem = EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude);
            if (eventSystem == null)
            {
                if (!_warnedNoEventSystem)
                {
                    _warnedNoEventSystem = true;
                    Debug.LogWarning("[VRBridge] No EventSystem in the scene; the prepared-training panel cannot be clicked in VR.");
                }

                return;
            }

            if (eventSystem.GetComponent<XRUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
            }

            // Matches the Office/Waste world UI bootstrap: only the XR module may drive the EventSystem in VR.
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (!(module is XRUIInputModule))
                {
                    module.enabled = false;
                }
            }

            foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                interactor.enableUIInteraction = true;
            }

            foreach (var interactor in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                interactor.enableUIInteraction = true;
            }
        }

        static bool IsVrLikely()
        {
            if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
            {
                return true;
            }

#pragma warning disable CS0618
            if (XRSettings.isDeviceActive)
            {
                return true;
            }
#pragma warning restore CS0618

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (display.running)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
