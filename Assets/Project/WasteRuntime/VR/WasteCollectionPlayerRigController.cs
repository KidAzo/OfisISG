using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Woi.DataHandler;
using Woi.InputSystem;
using Woi.Player;
using Woi.Settings;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Waste Collection: when VR is active, disables PC player root(s) and enables XR Origin;
    /// on PC, the opposite. Also toggles VR-only world UI without deactivating the shared
    /// WasteCollectionUI document. Hub, Quest, and the shared standalone selector
    /// decide the rig through <see cref="WasteCollectionPlatform"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class WasteCollectionPlayerRigController : MonoBehaviour
    {
        private const string PortingVariablePath =
            "Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset";

        private const string PcPlayerObjectName = "PC-Player";

        [Header("Rig roots (auto-filled when empty)")]
        [SerializeField] private List<GameObject> pcPlayerRoots = new();
        [SerializeField] private List<GameObject> xrOriginRoots = new();

        [Header("Porting")]
        [SerializeField] private ScriptableEnumPortingVariable portingVariable;
        [SerializeField] private AppMode fallbackMode = AppMode.PC;

        [Header("Timing")]
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool reapplyOnStart = true;
        [SerializeField] private bool ensurePcLocomotionAfterApply = true;
        [SerializeField] private bool logAppliedState;

        private void Awake()
        {
            EnsurePortingInitialized();
            StandalonePlatform.ApplyIfNoHubOverride();
            AutoResolveRigRootsIfNeeded();

            if (applyOnAwake)
                ApplyPlayerRigForCurrentMode();
        }

        private void Start()
        {
            if (!reapplyOnStart)
                return;

            EnsurePortingInitialized();
            StandalonePlatform.ApplyIfNoHubOverride();
            ApplyPlayerRigForCurrentMode();
        }

        [ContextMenu("Apply Player Rig For Current Mode")]
        public void ApplyPlayerRigForCurrentMode()
        {
            AutoResolveRigRootsIfNeeded();

            bool vrActive = UsesVrPresentation();
            bool pcActive = !vrActive;

            if (pcActive && pcPlayerRoots.Count > 1)
            {
                GameObject primaryPc = SelectPrimaryPcRoot(pcPlayerRoots);
                SetRootsActive(pcPlayerRoots, false);
                if (primaryPc != null)
                    primaryPc.SetActive(true);
            }
            else
            {
                SetRootsActive(pcPlayerRoots, pcActive);
            }

            SetRootsActive(xrOriginRoots, vrActive);
            ApplyWastePlatformUi(vrActive);

            if (logAppliedState)
            {
                Debug.Log(
                    $"[WasteCollectionPlayerRigController] vr={vrActive} → PC roots active={pcActive} ({pcPlayerRoots.Count}), " +
                    $"XR roots active={vrActive} ({xrOriginRoots.Count}).",
                    this);
            }

            if (pcActive && ensurePcLocomotionAfterApply)
                StartCoroutine(EnsurePcLocomotionAfterRigApplied());
        }

        private IEnumerator EnsurePcLocomotionAfterRigApplied()
        {
            const int maxWaitFrames = 300;
            for (int frame = 0;
                 frame < maxWaitFrames &&
                 (!ServiceLocator.TryGet(out IPlayerService _) ||
                  !ServiceLocator.TryGet(out InputManager _));
                 frame++)
            {
                yield return null;
            }

            if (UsesVrPresentation())
                yield break;

            GameObject pcRoot = ResolveActivePcRoot();
            if (pcRoot == null || !pcRoot.activeInHierarchy)
            {
                Debug.LogWarning("[WasteCollectionPlayerRigController] PC locomotion: no active PC-Player root.", this);
                yield break;
            }

            PlayerController controller = pcRoot.GetComponentInChildren<PlayerController>(true);
            if (controller == null)
            {
                Debug.LogError("[WasteCollectionPlayerRigController] PC locomotion: PlayerController not found.", this);
                yield break;
            }

            if (ServiceLocator.TryGet(out IPlayerService playerService))
            {
                playerService.RegisterPlayer(controller);
                playerService.SetPlayerInputEnabled(true);
            }

            if (ServiceLocator.TryGet(out InputManager inputManager))
                inputManager.EnsurePcGameplayInputEnabled();

            controller.ActivatePcLocomotion();

            RequestLoadingCameraRefresh();

            if (logAppliedState)
            {
                Debug.Log(
                    $"[WasteCollectionPlayerRigController] PC locomotion ensured on '{pcRoot.name}'.",
                    this);
            }
        }

        private GameObject ResolveActivePcRoot()
        {
            if (pcPlayerRoots == null || pcPlayerRoots.Count == 0)
                return null;

            if (pcPlayerRoots.Count > 1)
                return SelectPrimaryPcRoot(pcPlayerRoots);

            return pcPlayerRoots[0];
        }

        private void EnsurePortingInitialized()
        {
            if (portingVariable == null)
                ResolvePortingVariable();

            if (portingVariable != null)
                FirePlatformRuntime.TryInitialize(portingVariable);
        }

        private bool UsesVrPresentation()
        {
            return WasteCollectionPlatform.ShouldUseVrPresentation();
        }

        /// <summary>
        /// PC turns off VR world-space roots and the VR helpers that live on the shared
        /// WasteCollectionUI object. The shared UIDocument stays active.
        /// </summary>
        private static void ApplyWastePlatformUi(bool vrActive)
        {
            SetBehavioursEnabled<WasteWorldUiPresenter>(vrActive);
            SetBehavioursEnabled<WasteVrCounterHud>(vrActive);
            SetBehavioursEnabled<WasteVrUiSessionController>(vrActive);
            SetBehavioursEnabled<WasteVrExitInput>(vrActive);
            SetBehavioursEnabled<WasteVrLocomotionGate>(vrActive);
            SetBehavioursEnabled<WasteCollectionVrBootstrap>(vrActive);

            SetObjectsActive<SessionProfileWorldUiPresenter>(vrActive);
        }

        private static void SetBehavioursEnabled<T>(bool enabled) where T : Behaviour
        {
            T[] behaviours = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                T behaviour = behaviours[i];
                if (behaviour == null || behaviour.enabled == enabled)
                    continue;

                behaviour.enabled = enabled;
            }
        }

        private static void SetObjectsActive<T>(bool active) where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                GameObject root = component.gameObject;
                if (root.activeSelf == active)
                    continue;

                root.SetActive(active);
            }
        }

        private void AutoResolveRigRootsIfNeeded()
        {
            if (pcPlayerRoots == null)
                pcPlayerRoots = new List<GameObject>();

            if (xrOriginRoots == null)
                xrOriginRoots = new List<GameObject>();

            if (pcPlayerRoots.Count == 0)
                FindPcPlayerRoots(pcPlayerRoots);

            if (xrOriginRoots.Count == 0)
                FindXrOriginRoots(xrOriginRoots);
        }

        private static void FindPcPlayerRoots(List<GameObject> results)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || !go.scene.IsValid())
                    continue;

                if (!string.Equals(go.name, PcPlayerObjectName, StringComparison.Ordinal))
                    continue;

                if (HasXrOriginInHierarchy(go))
                    continue;

                if (!results.Contains(go))
                    results.Add(go);
            }
        }

        private static void FindXrOriginRoots(List<GameObject> results)
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return;

            Array found = Resources.FindObjectsOfTypeAll(originType);
            for (int i = 0; i < found.Length; i++)
            {
                if (found.GetValue(i) is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid())
                    continue;

                if (!results.Contains(go))
                    results.Add(go);
            }
        }

        private static bool HasXrOriginInHierarchy(GameObject root)
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return false;

            return root.GetComponentInChildren(originType, true) != null;
        }

        /// <summary>
        /// FireModule_Office has two PC-Player instances (fire drill under 01_Player and waste under =======WasteCollection=====).
        /// </summary>
        private static GameObject SelectPrimaryPcRoot(List<GameObject> roots)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null && IsUnderWasteCollection(roots[i]))
                    return roots[i];
            }

            return roots.Count > 0 ? roots[0] : null;
        }

        private static bool IsUnderWasteCollection(GameObject root)
        {
            Transform t = root.transform;
            while (t != null)
            {
                if (t.name.IndexOf("WasteCollection", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                t = t.parent;
            }

            return false;
        }

        private static void SetRootsActive(List<GameObject> roots, bool active)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Count; i++)
            {
                GameObject root = roots[i];
                if (root == null || root.activeSelf == active)
                    continue;

                root.SetActive(active);
            }
        }

        private static void RequestLoadingCameraRefresh()
        {
            LoadingScreenController[] controllers = UnityEngine.Object.FindObjectsByType<LoadingScreenController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i]?.RequestDelayedDisplayFallbackRefresh();
            }
        }

        private void ResolvePortingVariable()
        {
#if UNITY_EDITOR
            portingVariable =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(PortingVariablePath);
#endif
        }
    }
}
