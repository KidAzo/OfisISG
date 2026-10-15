using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using Woi.Settings;

/// <summary>
/// Moves Hazard Hunt VR canvases with the live XR head.
/// World-space canvases under Gameplay/UI keep a pose baked at the spawn point.
/// Their <see cref="Canvas.worldCamera"/> is only the raycast camera, so it does not move them.
/// Persistent HUD canvases are posed every frame from the active XR camera's yaw.
/// Exit remains a current-head popup. The final result remains at its authored world pose,
/// and the XR Origin is moved to the authored ResultPos viewing marker.
/// PC canvases are left where they are.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class HazardVrUiPresentation : MonoBehaviour
{
    const string XrRootName = "-------------XR------------";
    const string GameplayRootName = "Gameplay";
    const string HudCanvasName = "VRCanvas";
    const string InfoCanvasName = "VRCanvas (1)";
    const string ExitCanvasName = "VRCanvas (2)";
    const string ScoreRootName = "Counter";
    const string HazardInfoContainerName = "VRPopupContainer";
    const string LeaderboardName = "LeaderBoardVRPanel";
    const string ResultName = "HazardSystemResult-VR";
    const string ResultPoseName = "ResultPos";
    const float HudFallbackDistance = 1f;
    const float LogTimeoutSeconds = 2f;

    struct YawBinding
    {
        public Transform Target;
        public Vector3 LocalOffset;
        public float YawDelta;
        public bool PreserveVerticalOffset;
        public bool Captured;
    }

    static HazardVrUiPresentation _instance;

    Transform _head;
    Camera _xrCamera;
    Camera _assignedCamera;
    Canvas _hudCanvas;
    Canvas _infoCanvas;
    Canvas _exitCanvas;
    Canvas _resultCanvas;
    Transform _resultRoot;
    Transform _leaderboard;
    YawBinding _hud;
    YawBinding _info;
    YawBinding _score;
    YawBinding _hazardInfo;
    bool _searched;
    bool _logged;
    Vector3 _authoredResultPosition;
    Quaternion _authoredResultRotation;
    bool _authoredResultPoseCaptured;
    Transform _resultPose;
    bool _resultPoseSearchDone;
    bool _resultAreaPrepared;
    bool _resultPoseErrorLogged;
    bool _exitWasActive;
    float _createdAt;

    public static void NotifyVrRigApplied()
    {
        if (!HazardHubLaunch.UseVrPresentation || _instance != null)
            return;

        var host = new GameObject(nameof(HazardVrUiPresentation));
        GameObject xrRoot = FindActiveXrRoot();
        Scene scene = xrRoot != null ? xrRoot.scene : SceneManager.GetActiveScene();
        if (scene.IsValid())
            SceneManager.MoveGameObjectToScene(host, scene);

        host.AddComponent<HazardVrUiPresentation>();
    }

    /// <summary>
    /// Puts an opened panel on the same yaw-facing HUD plane as VRCanvas.
    /// Does not parent it, so it stays put for pointer hits after it appears.
    /// </summary>
    public static void PlaceWithHudPose(Transform panel)
    {
        if (!CanPlace(panel))
            return;

        NotifyVrRigApplied();
        if (_instance == null || !_instance.Prepare())
            return;

        _instance.PoseLikeHud(panel);
    }

    /// <summary>
    /// Leaves HazardSystemResult-VR on its authored world pose and moves the live XR Origin
    /// so the headset stands at the scene's ResultPos marker.
    /// </summary>
    public static void PlaceOpenedResult(Transform resultRoot)
    {
        if (!CanPlace(resultRoot))
            return;

        if (resultRoot.name.IndexOf("VR", StringComparison.OrdinalIgnoreCase) < 0
            && resultRoot.name.IndexOf("XR", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        NotifyVrRigApplied();
        if (_instance == null || !_instance.Prepare())
            return;

        _instance._resultRoot = resultRoot;
        _instance._resultCanvas = resultRoot.GetComponentInChildren<Canvas>(true);
        _instance.RememberAuthoredResultPose();
        _instance._resultPose = FindResultPose(resultRoot.gameObject.scene);
        _instance.PrepareResultArea();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _createdAt = Time.unscaledTime;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void LateUpdate()
    {
        if (!HazardHubLaunch.UseVrPresentation)
            return;

        if (!Prepare())
        {
            LogIfTimedOut();
            return;
        }

        ApplyBinding(_hud, _head);
        ApplyBinding(_info, _head);
        ApplyBinding(_score, _head);
        ApplyBinding(_hazardInfo, _head);
        PlaceExitIfJustOpened();
        PlaceResultIfJustOpened();
        LogOnce();
    }

    bool Prepare()
    {
        if (!HazardHubLaunch.UseVrPresentation)
            return false;

        if (!ResolveHead())
            return false;

        if (!_searched)
            Discover();

        AssignKnownCameras();
        return true;
    }

    bool ResolveHead()
    {
        if (_xrCamera != null && _xrCamera.isActiveAndEnabled)
        {
            _head = _xrCamera.transform;
            return true;
        }

        _xrCamera = FindXrHeadCamera();
        _head = _xrCamera != null ? _xrCamera.transform : null;
        return _head != null;
    }

    void Discover()
    {
        _searched = true;
        _hudCanvas = FindCanvasInScene(HudCanvasName, _head.gameObject.scene);
        _infoCanvas = FindCanvas(InfoCanvasName);
        _exitCanvas = FindCanvas(ExitCanvasName);
        _leaderboard = FindExact(LeaderboardName);
        _resultRoot = FindExact(ResultName);
        _resultCanvas = _resultRoot != null ? _resultRoot.GetComponentInChildren<Canvas>(true) : null;

        // These are the two persistent gameplay UIs. Track their exact roots as well as
        // their canvas so they still follow if canvas discovery happens before/against a
        // different loaded Office scene. Popup is instantiated beneath the container.
        Transform scoreRoot = FindVrTarget(ScoreRootName, HudCanvasName, true);
        Transform hazardInfoContainer = FindVrTarget(HazardInfoContainerName, HudCanvasName, false);

        if (_hudCanvas != null)
            _hud = Capture(_hudCanvas.transform, _head);
        if (_infoCanvas != null)
            _info = Capture(_infoCanvas.transform, _head);
        if (scoreRoot != null && _hudCanvas != null && _hud.Captured)
            _score = CaptureRelativeToHud(scoreRoot, _hudCanvas.transform, _hud);
        if (hazardInfoContainer != null && _hudCanvas != null && _hud.Captured)
            _hazardInfo = CaptureRelativeToHud(hazardInfoContainer, _hudCanvas.transform, _hud);
    }

    void AssignKnownCameras()
    {
        if (_assignedCamera == _xrCamera)
            return;

        AssignWorldCamera(_hudCanvas != null ? _hudCanvas.transform : null);
        AssignWorldCamera(_infoCanvas != null ? _infoCanvas.transform : null);
        AssignWorldCamera(_exitCanvas != null ? _exitCanvas.transform : null);
        AssignWorldCamera(_resultRoot);
        _assignedCamera = _xrCamera;
    }

    void AssignWorldCamera(Transform root)
    {
        if (root == null || _xrCamera == null)
            return;

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = _xrCamera;
        }
    }

    void LogOnce()
    {
        if (_logged)
            return;

        _logged = true;
        Debug.Log(
            "[HazardVRUI]\n" +
            "xrCamera=" + Describe(_xrCamera) + "\n" +
            "cameraMain=" + Describe(Camera.main) + "\n" +
            "scoreUi=" + Describe(_score.Target) + "\n" +
            "hazardInfoUi=" + Describe(_hazardInfo.Target) + "\n" +
            "vrCanvas1Camera=" + Describe(WorldCamera(_infoCanvas)) + "\n" +
            "vrCanvas2Camera=" + Describe(WorldCamera(_exitCanvas)) + "\n" +
            "leaderboardCamera=" + LeaderboardCamera(_leaderboard) + "\n" +
            "finishUi=" + NameOf(_resultRoot) + "\n" +
            "finishUiCanvas=" + NameOf(_resultCanvas) + "\n" +
            "finishUiPose=" + FullPath(_resultRoot) + "\n" +
            "finishViewerPose=" + FullPath(_resultPose),
            this);
    }

    void PlaceExitIfJustOpened()
    {
        if (_exitCanvas == null)
            return;

        Transform exitPanel = _exitCanvas.transform.Find("ExitPanel");
        bool active = exitPanel != null && exitPanel.gameObject.activeInHierarchy;
        if (active && !_exitWasActive)
            PoseLikeHud(_exitCanvas.transform);

        _exitWasActive = active;
    }

    void PlaceResultIfJustOpened()
    {
        if (_resultRoot == null)
            return;

        bool active = _resultRoot.gameObject.activeInHierarchy;
        if (active && !_resultAreaPrepared)
            PrepareResultArea();
    }

    void PoseLikeHud(Transform panel)
    {
        YawBinding source = _hud.Captured ? _hud : _info;
        if (!source.Captured)
        {
            source.LocalOffset = new Vector3(0f, 0f, HudFallbackDistance);
            source.YawDelta = 0f;
            source.PreserveVerticalOffset = false;
            source.Captured = true;
        }

        source.Target = panel;
        ApplyBinding(source, _head);
        AssignWorldCamera(panel);
    }

    void RememberAuthoredResultPose()
    {
        if (_authoredResultPoseCaptured || _resultRoot == null)
            return;

        _authoredResultPosition = _resultRoot.position;
        _authoredResultRotation = _resultRoot.rotation;
        _authoredResultPoseCaptured = true;
    }

    void RestoreAuthoredResultPose()
    {
        if (!_authoredResultPoseCaptured || _resultRoot == null)
            return;

        _resultRoot.SetPositionAndRotation(_authoredResultPosition, _authoredResultRotation);
    }

    void PrepareResultArea()
    {
        if (_resultAreaPrepared || _resultRoot == null || _head == null)
            return;

        RememberAuthoredResultPose();
        RestoreAuthoredResultPose();

        if (_resultPose == null && !_resultPoseSearchDone)
        {
            _resultPose = FindResultPose(_resultRoot.gameObject.scene);
            _resultPoseSearchDone = true;
        }

        XROrigin xrOrigin = ResolveXrOrigin();
        if (_resultPose == null || xrOrigin == null || xrOrigin.Origin == null)
        {
            if (!_resultPoseErrorLogged)
            {
                _resultPoseErrorLogged = true;
                Debug.LogError(
                    "[HazardResultPose] Cannot enter authored result area. result=" + NameOf(_resultRoot) +
                    " pose=" + NameOf(_resultPose) + " xrOrigin=" + NameOf(xrOrigin),
                    this);
            }
            return;
        }

        GameObject xrRoot = FindActiveXrRoot();
        Transform rig = xrRoot != null ? xrRoot.transform : xrOrigin.transform;
        CharacterController[] controllers = rig.GetComponentsInChildren<CharacterController>(true);
        CharacterController parentController = rig.GetComponentInParent<CharacterController>();
        bool parentControllerWasEnabled = parentController != null
            && parentController.enabled
            && Array.IndexOf(controllers, parentController) < 0;
        bool[] controllerStates = new bool[controllers.Length];
        for (int i = 0; i < controllers.Length; i++)
        {
            controllerStates[i] = controllers[i] != null && controllers[i].enabled;
            if (controllerStates[i])
                controllers[i].enabled = false;
        }
        if (parentControllerWasEnabled)
            parentController.enabled = false;

        Rigidbody[] bodies = rig.GetComponentsInChildren<Rigidbody>(true);
        Rigidbody parentBody = rig.GetComponentInParent<Rigidbody>();
        try
        {
            StopRigidbodies(bodies);
            if (parentBody != null && Array.IndexOf(bodies, parentBody) < 0)
                StopRigidbody(parentBody);

            Physics.SyncTransforms();

            Vector3 forward = _resultPose.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            Vector3 cameraBefore = _head.position;
            bool aligned = xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
            bool moved = MoveXrCameraTo(_resultPose.position, xrOrigin);
            Physics.SyncTransforms();
            RestoreAuthoredResultPose();
            if (moved)
                _resultAreaPrepared = true;
            AssignWorldCamera(_resultRoot);

            string message =
                "[HazardResultPose] pose=" + FullPath(_resultPose) +
                " targetPosition=" + _resultPose.position.ToString("F3") +
                " targetYaw=" + _resultPose.eulerAngles.y.ToString("F1") +
                " cameraBefore=" + cameraBefore.ToString("F3") +
                " cameraAfter=" + (_head != null ? _head.position.ToString("F3") : "none") +
                " aligned=" + aligned +
                " moved=" + moved +
                " result=" + FullPath(_resultRoot) +
                " resultPosition=" + _resultRoot.position.ToString("F3");
            if (aligned && moved)
                Debug.Log(message, this);
            else if (!_resultPoseErrorLogged)
            {
                _resultPoseErrorLogged = true;
                Debug.LogError(message, this);
            }
        }
        finally
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                    controllers[i].enabled = controllerStates[i];
            }
            if (parentControllerWasEnabled)
                parentController.enabled = true;
        }
    }

    bool MoveXrCameraTo(Vector3 worldPosition, XROrigin xrOrigin)
    {
        if (xrOrigin.Camera != null && xrOrigin.MoveCameraToWorldLocation(worldPosition))
            return true;

        Transform originTransform = xrOrigin.Origin != null ? xrOrigin.Origin.transform : xrOrigin.transform;
        Transform camera = xrOrigin.Camera != null ? xrOrigin.Camera.transform : _head;
        if (originTransform == null || camera == null)
            return false;

        Vector3 cameraOffset = camera.position - originTransform.position;
        originTransform.position = worldPosition - cameraOffset;
        return true;
    }

    XROrigin ResolveXrOrigin()
    {
        if (_head != null)
        {
            XROrigin parentOrigin = _head.GetComponentInParent<XROrigin>(true);
            if (parentOrigin != null)
                return parentOrigin;
        }

        GameObject xrRoot = FindActiveXrRoot();
        return xrRoot != null ? xrRoot.GetComponentInChildren<XROrigin>(true) : null;
    }

    static void StopRigidbodies(Rigidbody[] bodies)
    {
        if (bodies == null)
            return;

        for (int i = 0; i < bodies.Length; i++)
            StopRigidbody(bodies[i]);
    }

    static void StopRigidbody(Rigidbody body)
    {
        if (body == null || body.isKinematic)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    void LogIfTimedOut()
    {
        if (_logged || Time.unscaledTime - _createdAt < LogTimeoutSeconds)
            return;

        LogOnce();
    }

    static bool CanPlace(Transform panel)
    {
        return HazardHubLaunch.UseVrPresentation && panel != null;
    }

    static YawBinding Capture(Transform target, Transform head)
    {
        // Measure the authored offset from the rig eye, not the tracked head.
        // A sideways look at startup must not bake a permanent side offset.
        Transform reference = head.parent != null ? head.parent : head;
        Quaternion yaw = YawRotation(reference);
        Vector3 local = Quaternion.Inverse(yaw) * (target.position - reference.position);
        local.y = 0f;
        if (local.sqrMagnitude < 0.04f)
            local = new Vector3(0f, 0f, HudFallbackDistance);

        return new YawBinding
        {
            Target = target,
            LocalOffset = local,
            YawDelta = 0f,
            PreserveVerticalOffset = false,
            Captured = true
        };
    }

    static YawBinding CaptureRelativeToHud(Transform target, Transform hud, YawBinding hudBinding)
    {
        Quaternion hudYaw = YawRotation(hud);
        Vector3 localFromHud = Quaternion.Inverse(hudYaw) * (target.position - hud.position);
        return new YawBinding
        {
            Target = target,
            LocalOffset = hudBinding.LocalOffset + localFromHud,
            YawDelta = Mathf.DeltaAngle(hudYaw.eulerAngles.y, target.eulerAngles.y),
            PreserveVerticalOffset = true,
            Captured = true
        };
    }

    static void ApplyBinding(YawBinding binding, Transform head)
    {
        if (!binding.Captured || binding.Target == null || head == null)
            return;

        Quaternion yaw = YawRotation(head);
        Vector3 offset = binding.LocalOffset;
        if (!binding.PreserveVerticalOffset)
            offset.y = 0f;
        Quaternion rotation = yaw * Quaternion.Euler(0f, binding.YawDelta, 0f);
        binding.Target.SetPositionAndRotation(head.position + (yaw * offset), rotation);
    }

    static Quaternion YawRotation(Transform head)
    {
        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = head.parent != null ? head.parent.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    static Camera FindXrHeadCamera()
    {
        GameObject xrRoot = FindActiveXrRoot();
        if (xrRoot == null)
            return null;

        Camera[] cameras = xrRoot.GetComponentsInChildren<Camera>(false);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                continue;

            Transform cameraOffset = camera.transform.parent;
            Transform xrOrigin = cameraOffset != null ? cameraOffset.parent : null;
            if (camera.name == "Main Camera"
                && camera.CompareTag("MainCamera")
                && cameraOffset != null
                && cameraOffset.name == "Camera Offset"
                && xrOrigin != null
                && xrOrigin.name.StartsWith("XR Origin", StringComparison.Ordinal))
                return camera;
        }

        return null;
    }

    static GameObject FindActiveXrRoot()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == XrRootName && candidate.gameObject.activeInHierarchy)
                return candidate.gameObject;
        }

        return null;
    }

    static Transform FindVrTarget(string objectName, string canvasName, bool requireDirectCanvasParent)
    {
        if (_instance == null || _instance._head == null)
            return null;

        Scene xrScene = _instance._head.gameObject.scene;
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null
                || candidate.name != objectName
                || candidate.gameObject.scene != xrScene
                || !HasAncestor(candidate, GameplayRootName))
                continue;

            Transform canvas = FindAncestor(candidate, canvasName);
            if (canvas == null)
                continue;

            if (!requireDirectCanvasParent || candidate.parent == canvas)
                return candidate;
        }

        return null;
    }

    static Transform FindResultPose(Scene scene)
    {
        Transform underGameplay = FindExactInScene(ResultPoseName, scene);
        if (underGameplay != null)
            return underGameplay;

        if (!scene.IsValid())
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == ResultPoseName && candidate.gameObject.scene == scene)
                return candidate;
        }

        return null;
    }

    static Transform FindExactInScene(string objectName, Scene scene)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null
                && candidate.name == objectName
                && candidate.gameObject.scene == scene
                && HasAncestor(candidate, GameplayRootName))
                return candidate;
        }

        return null;
    }

    static Canvas FindCanvas(string objectName)
    {
        Transform transform = FindExact(objectName);
        return transform != null ? transform.GetComponent<Canvas>() : null;
    }

    static Canvas FindCanvasInScene(string objectName, Scene scene)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null
                && candidate.name == objectName
                && candidate.gameObject.scene == scene
                && HasAncestor(candidate, GameplayRootName))
                return candidate.GetComponent<Canvas>();
        }

        return null;
    }

    static Transform FindExact(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform fallback = null;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
                continue;

            if (fallback == null)
                fallback = candidate;

            if (HasAncestor(candidate, GameplayRootName))
                return candidate;
        }

        return fallback;
    }

    static GameObject FindNamed(string objectName)
    {
        Transform transform = FindExact(objectName);
        return transform != null ? transform.gameObject : null;
    }

    static bool HasAncestor(Transform transform, string ancestorName)
    {
        return FindAncestor(transform, ancestorName) != null;
    }

    static Transform FindAncestor(Transform transform, string ancestorName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == ancestorName)
                return current;

            current = current.parent;
        }

        return null;
    }

    static string LeaderboardCamera(Transform leaderboard)
    {
        if (leaderboard == null)
            return "none";

        Canvas canvas = leaderboard.GetComponent<Canvas>();
        if (canvas == null)
            canvas = leaderboard.GetComponentInChildren<Canvas>(true);

        return canvas != null ? Describe(canvas.worldCamera) : "none";
    }

    static Camera WorldCamera(Canvas canvas)
    {
        return canvas != null ? canvas.worldCamera : null;
    }

    static string Describe(Camera camera)
    {
        if (camera == null)
            return "none";

        Transform parent = camera.transform.parent;
        return parent != null ? parent.name + "/" + camera.name : camera.name;
    }

    static string Describe(Transform transform)
    {
        if (transform == null)
            return "none";

        return transform.parent != null ? transform.parent.name + "/" + transform.name : transform.name;
    }

    static string NameOf(UnityEngine.Object target)
    {
        return target != null ? target.name : "none";
    }

    static string FullPath(Transform transform)
    {
        if (transform == null)
            return "none";

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
