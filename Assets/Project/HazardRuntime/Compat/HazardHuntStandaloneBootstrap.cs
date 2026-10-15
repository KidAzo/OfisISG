using System.Collections;
using System.Reflection;
using UnityEngine;
using Woi.HazardSystem;
using Woi.InputSystem;
using Woi.OfficeFire;

namespace Woi.HazardSystem.Compat
{
    public class HazardHuntStandaloneBootstrap : MonoBehaviour
    {
        const string ResultPrefabPath = "Assets/Project/_scripts/HazardSystem/PrefabsUI/HazardSystemResult.prefab";

        [SerializeField] GameObject resultUiPrefab;
        [SerializeField] GameObject pcUiRoot;
        [SerializeField] GameObject[] vrGameplayUiRoots;
        [SerializeField] GameObject vrResultUiRoot;

        void Start()
        {
            ApplyPlatformUiRoots();
            ExitPanelController.EnsurePcHost(pcUiRoot);
            HazardInfoPopupPresenter.EnsureInstalled();
            EnsureDetector();
            EnsureResultUi();
            StartCoroutine(SyncPcInputWhenReady());
        }

        void ApplyPlatformUiRoots()
        {
            bool xr = Woi.Settings.HazardHubLaunch.UseVrPresentation;

            if (pcUiRoot != null)
                pcUiRoot.SetActive(!xr);

            if (vrGameplayUiRoots != null)
            {
                for (int i = 0; i < vrGameplayUiRoots.Length; i++)
                {
                    if (vrGameplayUiRoots[i] != null)
                        vrGameplayUiRoots[i].SetActive(xr);
                }
            }

            if (vrResultUiRoot != null)
                vrResultUiRoot.SetActive(false);

            Woi.Settings.HazardPlatformUiDriver.HideLegacyOfficeLoginIfHubLaunch();
        }

        IEnumerator SyncPcInputWhenReady()
        {
            const int maxFrames = 300;
            for (int i = 0; i < maxFrames; i++)
            {
                if (FindFirstObjectByType<InputManager>(FindObjectsInactive.Include) != null)
                {
                    OfficeFireInputSync.RequestDelayedSync(this, "HazardHunt Office");
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[HazardHuntStandaloneBootstrap] InputManager not found after Office load — PC move/look/sprint will not work.",
                this);
        }

        void EnsureDetector()
        {
            bool vr = Woi.Settings.HazardHubLaunch.UseVrPresentation;
            HazardDetector detector = FindFirstObjectByType<HazardDetector>(FindObjectsInactive.Include);
            if (detector == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;

                detector = CreateDetector(cam, vr);
            }

            if (vr)
                UseVrConfirm(detector);
        }

        static HazardDetector CreateDetector(Camera cam, bool vr)
        {
            GameObject host = cam.gameObject;
            CameraCenterRayProvider ray = host.GetComponent<CameraCenterRayProvider>()
                ?? host.AddComponent<CameraCenterRayProvider>();
            InteractionInputBase input = vr
                ? host.GetComponent<VrTriggerInteractionInput>() ?? host.AddComponent<VrTriggerInteractionInput>()
                : host.GetComponent<PcInteractionInput>() ?? host.AddComponent<PcInteractionInput>();
            HazardDetector detector = host.AddComponent<HazardDetector>();
            AssignDetector(detector, ray, input, cam);
            return detector;
        }

        static void UseVrConfirm(HazardDetector detector)
        {
            if (detector == null)
                return;

            PcInteractionInput[] pcInputs = detector.GetComponents<PcInteractionInput>();
            for (int i = 0; i < pcInputs.Length; i++)
            {
                if (pcInputs[i] != null)
                    pcInputs[i].enabled = false;
            }

            VrTriggerInteractionInput vrInput = detector.GetComponent<VrTriggerInteractionInput>()
                ?? detector.gameObject.AddComponent<VrTriggerInteractionInput>();
            CameraCenterRayProvider ray = detector.GetComponent<CameraCenterRayProvider>();
            if (ray == null)
            {
                ray = detector.gameObject.AddComponent<CameraCenterRayProvider>();
                Camera cam = Camera.main;
                if (cam != null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(CameraCenterRayProvider).GetField("targetCamera", flags)?.SetValue(ray, cam);
                }
            }

            const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            bool wasEnabled = detector.enabled;
            detector.enabled = false;
            typeof(HazardDetector).GetField("inputProvider", fieldFlags)?.SetValue(detector, vrInput);
            object currentRay = typeof(HazardDetector).GetField("rayProvider", fieldFlags)?.GetValue(detector);
            if (currentRay == null)
                typeof(HazardDetector).GetField("rayProvider", fieldFlags)?.SetValue(detector, ray);
            detector.enabled = wasEnabled;
        }

        static void AssignDetector(
            HazardDetector detector,
            CameraCenterRayProvider ray,
            InteractionInputBase input,
            Camera cam)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (ray != null && cam != null)
                typeof(CameraCenterRayProvider).GetField("targetCamera", flags)?.SetValue(ray, cam);
            typeof(HazardDetector).GetField("rayProvider", flags)?.SetValue(detector, ray);
            typeof(HazardDetector).GetField("inputProvider", flags)?.SetValue(detector, input);
            typeof(HazardDetector).GetField("maxDistance", flags)?.SetValue(detector, 40f);
            typeof(HazardDetector).GetField("hazardLayerMask", flags)?.SetValue(detector, (LayerMask)(~0));
        }

        void EnsureResultUi()
        {
            SetVrResultControllersActive(false);

            var result = FindFirstObjectByType<HazardResultController>(FindObjectsInactive.Include);

            if (Woi.Settings.HazardHubLaunch.UseVrPresentation)
            {
                result?.BindPlatformResultUi();
                return;
            }

            var existingPc = FindExistingPcResultInstance();
            if (existingPc != null)
            {
                result?.AssignPcResultUi(existingPc);
                return;
            }

            GameObject prefab = resultUiPrefab;
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ResultPrefabPath);
#endif
            if (prefab == null)
            {
                Debug.LogError(
                    "[HazardHuntStandaloneBootstrap] HazardSystemResult.prefab could not be loaded for PC result UI.",
                    this);
                return;
            }

            var instance = Instantiate(prefab);
            instance.name = "HazardSystemResult";
            var pcController = instance.GetComponent<HazardSystemUIController>()
                ?? instance.GetComponentInChildren<HazardSystemUIController>(true);
            instance.SetActive(false);

            if (result != null && pcController != null)
                result.AssignPcResultUi(pcController);
        }

        static bool IsVrResultController(HazardSystemUIController ui)
        {
            return ui != null && (ui.gameObject.name.Contains("VR") || ui.gameObject.name.Contains("XR"));
        }

        static HazardSystemUIController FindExistingPcResultInstance()
        {
            var found = FindObjectsByType<HazardSystemUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !IsVrResultController(found[i]) && found[i].gameObject.name == "HazardSystemResult")
                    return found[i];
            }

            return null;
        }

        static void SetVrResultControllersActive(bool active)
        {
            var found = FindObjectsByType<HazardSystemUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (IsVrResultController(found[i]))
                    found[i].gameObject.SetActive(active);
            }
        }
    }
}
