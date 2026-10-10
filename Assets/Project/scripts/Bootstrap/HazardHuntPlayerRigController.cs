using UnityEngine;
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

    [SerializeField] GameObject pcRoot;
    [SerializeField] GameObject xrRoot;

    bool _resolved;
    string _lastLog;

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
