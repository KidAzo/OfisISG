using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Woi.Settings;

/// <summary>
/// Switches the Office Hazard gameplay rigs after the scene is loaded.
/// PC keeps <c>-------------PlayerPC------</c> active. VR/Quest keeps
/// <c>-------------XR------------</c> active. Only one of those roots is
/// active, so only one MainCamera renders. Does not search every frame.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class HazardHuntPlayerRigController : MonoBehaviour
{
    const string PcRootName = "-------------PlayerPC------";
    const string XrRootName = "-------------XR------------";
    const string XrCameraPath = "XRPlayer/XR Origin (XR Rig) (1)/Camera Offset/Main Camera";
    const int VrRendererIndex = 1;
    static readonly string[] VrRendererNames = { "PC_VR_Renderer", "Mobile_VR_Renderer" };

    [SerializeField] GameObject pcRoot;
    [SerializeField] GameObject xrRoot;

    bool _resolved;
    string _lastLog;
    string _lastRendererLog;
    bool _outlineRuntimeLogged;

    void Awake()
    {
        Apply();
    }

    void Start()
    {
        Apply();
    }

    /// <summary>
    /// Applies every loaded controller after the office scene exists and the
    /// Hub platform override has been written. Creates one controller if the
    /// scene has not been saved with this component yet.
    /// </summary>
    public static void ApplyLoaded()
    {
        HazardHuntPlayerRigController[] controllers = Object.FindObjectsByType<HazardHuntPlayerRigController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (controllers.Length == 0)
        {
            Scene office = SceneManager.GetSceneByName("Office");
            if (!office.IsValid() || !office.isLoaded)
                office = SceneManager.GetActiveScene();

            var host = new GameObject("HazardHuntPlayerRigController");
            if (office.IsValid())
                SceneManager.MoveGameObjectToScene(host, office);

            controllers = new[] { host.AddComponent<HazardHuntPlayerRigController>() };
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].Apply();
        }
    }

    public void Apply()
    {
        if (!ResolveRootsOnce())
            return;

        HazardHubLaunch.ApplyEditorStandaloneFallback();
        bool vr = HazardHubLaunch.UseVrPresentation;
        SetRootActive(pcRoot, !vr);
        SetRootActive(xrRoot, vr);
        if (vr)
            HazardVrUiPresentation.NotifyVrRigApplied();
        ApplyRenderer(vr);

        string cameraName = ActiveCameraName(vr ? xrRoot : pcRoot);
        string log =
            "[HazardRig]\n" +
            "mode=" + (vr ? "XR" : "PC") + "\n" +
            "pcRoot=" + pcRoot.activeSelf + "\n" +
            "xrRoot=" + xrRoot.activeSelf + "\n" +
            "activeCamera=\"" + cameraName + "\"";

        if (log == _lastLog)
            return;

        _lastLog = log;
        Debug.Log(log, this);
    }

    /// <summary>
    /// Soft Outline on PC_Renderer is not Single Pass Instanced safe, and VR needs a thinner outline, so the
    /// Hazard XR camera uses the active pipeline's VR renderer at index 1: PC_VR_Renderer on PC_RPAsset,
    /// Mobile_VR_Renderer on Mobile_RPAsset. The PC camera is never touched and stays on the default renderer.
    /// </summary>
    void ApplyRenderer(bool vr)
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        string log;

        if (!vr)
        {
            string pcRenderer = pipeline != null && pipeline.rendererDataList.Length > 0 && pipeline.rendererDataList[0] != null
                ? pipeline.rendererDataList[0].name
                : "unknown";
            log = "[HazardRenderer]\nmode=PC\nrenderer=" + pcRenderer;
        }
        else
        {
            Transform cameraTransform = xrRoot.transform.Find(XrCameraPath);
            Camera xrCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (xrCamera == null)
            {
                log = "[HazardRenderer]\nmode=VR\nerror=XR camera not found at '" + XrRootName + "/" + XrCameraPath + "'. Renderer unchanged.";
                LogRendererOnce(log, true);
                return;
            }

            ScriptableRendererData vrData = pipeline != null && pipeline.rendererDataList.Length > VrRendererIndex
                ? pipeline.rendererDataList[VrRendererIndex]
                : null;
            if (vrData == null || System.Array.IndexOf(VrRendererNames, vrData.name) < 0)
            {
                log = "[HazardRenderer]\nmode=VR\ncamera=" + xrCamera.name +
                      "\nwarning=pipeline '" + (pipeline != null ? pipeline.name : "none") +
                      "' has no " + string.Join("/", VrRendererNames) + " at index " + VrRendererIndex +
                      " (found " + (vrData != null ? vrData.name : "none") + "). Renderer unchanged.";
                LogRendererOnce(log, true);
                LogOutlineRuntimeOnce(xrCamera, pipeline);
                return;
            }

            xrCamera.GetUniversalAdditionalCameraData().SetRenderer(VrRendererIndex);
            log = "[HazardRenderer]\nmode=VR\ncamera=" + xrCamera.name +
                  "\nrendererIndex=" + VrRendererIndex +
                  "\nrenderer=" + vrData.name +
                  "\nfeatures=" + DescribeFeatures(vrData);
            LogRendererOnce(log, false);
            LogOutlineRuntimeOnce(xrCamera, pipeline);
            return;
        }

        LogRendererOnce(log, false);
    }

    /// <summary>
    /// Temporary audit log: the renderer and Fast Outline settings the XR camera actually renders with.
    /// Linework types are read by reflection so this assembly does not reference Linework.
    /// </summary>
    void LogOutlineRuntimeOnce(Camera camera, UniversalRenderPipelineAsset pipeline)
    {
        if (_outlineRuntimeLogged)
            return;
        _outlineRuntimeLogged = true;

        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        var cameraData = camera.GetUniversalAdditionalCameraData();
        object rawIndex = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", Flags)?.GetValue(cameraData);
        int rendererIndex = rawIndex is int i ? i : -1;
        int resolvedIndex = rendererIndex;
        if (resolvedIndex < 0 && pipeline != null)
        {
            object rawDefault = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", Flags)?.GetValue(pipeline);
            resolvedIndex = rawDefault is int d ? d : 0;
        }

        ScriptableRendererData rendererData = pipeline != null && resolvedIndex >= 0 && resolvedIndex < pipeline.rendererDataList.Length
            ? pipeline.rendererDataList[resolvedIndex]
            : null;

        string settingsAsset = "none";
        string width = "n/a";
        string minWidth = "n/a";
        if (rendererData != null)
        {
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "FastOutline")
                    continue;

                var settings = feature.GetType().GetField("settings", Flags)?.GetValue(feature) as Object;
                settingsAsset = settings != null ? settings.name : "null";
                var outlines = settings != null
                    ? settings.GetType().GetProperty("Outlines", Flags)?.GetValue(settings) as System.Collections.IList
                    : null;
                if (outlines != null && outlines.Count > 0 && outlines[0] != null)
                {
                    width = outlines[0].GetType().GetField("width", Flags)?.GetValue(outlines[0])?.ToString() ?? "n/a";
                    minWidth = outlines[0].GetType().GetField("minWidth", Flags)?.GetValue(outlines[0])?.ToString() ?? "n/a";
                }
                break;
            }
        }

        bool xr = UnityEngine.XR.XRSettings.enabled;
        Debug.Log(
            "[HazardOutlineRuntime]\ncamera=" + camera.name +
            "\nrendererIndex=" + rendererIndex + (rendererIndex < 0 ? " (default=" + resolvedIndex + ")" : "") +
            "\nrendererAsset=" + (rendererData != null ? rendererData.name : "none") +
            "\npipeline=" + (pipeline != null ? pipeline.name : "none") +
            " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
            "\nsettingsAsset=" + settingsAsset +
            "\nconfiguredWidth=" + width +
            "\nminimumWidth=" + minWidth +
            "\nrenderScale=" + (pipeline != null ? pipeline.renderScale.ToString() : "n/a") +
            " eyeTextureScale=" + (xr ? UnityEngine.XR.XRSettings.eyeTextureResolutionScale.ToString() : "n/a") +
            "\npixelWidth=" + (xr ? UnityEngine.XR.XRSettings.eyeTextureWidth : camera.pixelWidth) +
            "\npixelHeight=" + (xr ? UnityEngine.XR.XRSettings.eyeTextureHeight : camera.pixelHeight),
            this);
    }

    void LogRendererOnce(string log, bool warning)
    {
        if (log == _lastRendererLog)
            return;

        _lastRendererLog = log;
        if (warning)
            Debug.LogWarning(log, this);
        else
            Debug.Log(log, this);
    }

    static string DescribeFeatures(ScriptableRendererData data)
    {
        var features = data.rendererFeatures;
        var names = new string[features.Count];
        for (int i = 0; i < features.Count; i++)
            names[i] = features[i] != null ? features[i].GetType().Name + (features[i].isActive ? "" : "(off)") : "null";
        return string.Join(",", names);
    }

    bool ResolveRootsOnce()
    {
        if (_resolved)
            return pcRoot != null && xrRoot != null;

        _resolved = true;
        Scene scene = gameObject.scene;
        if (pcRoot == null)
            pcRoot = FindNamed(scene, PcRootName);
        if (xrRoot == null)
            xrRoot = FindNamed(scene, XrRootName);

        if (pcRoot != null && xrRoot != null)
            return true;

        Debug.LogError(
            "[HazardRig] Missing rig root. pc=" + NameOf(pcRoot) +
            " xr=" + NameOf(xrRoot) +
            ". Expected '" + PcRootName + "' and '" + XrRootName + "'.",
            this);
        return false;
    }

    static void SetRootActive(GameObject root, bool active)
    {
        if (root != null && root.activeSelf != active)
            root.SetActive(active);
    }

    static string ActiveCameraName(GameObject root)
    {
        if (root == null || !root.activeInHierarchy)
            return "none";

        Camera[] cameras = root.GetComponentsInChildren<Camera>(false);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.enabled && camera.CompareTag("MainCamera"))
                return camera.gameObject.name;
        }

        return cameras.Length > 0 && cameras[0] != null ? cameras[0].gameObject.name : "none";
    }

    static GameObject FindNamed(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindNamed(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    static GameObject FindNamed(Transform transform, string objectName)
    {
        if (transform.name == objectName)
            return transform.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject found = FindNamed(transform.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    static string NameOf(GameObject root)
    {
        return root != null ? root.name : "null";
    }
}
