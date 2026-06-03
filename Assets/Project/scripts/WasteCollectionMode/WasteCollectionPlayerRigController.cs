using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Woi.InputSystem;
using Woi.Player;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Waste Collection: when <see cref="AppMode.XR"/> is active, disables PC player root(s) and enables XR Origin;
    /// on PC, the opposite. Uses the same <see cref="FirePlatformRuntime"/> / PortingVariable as the Fire module.
    /// </summary>
    [DisallowMultipleComponent]
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
            AutoResolveRigRootsIfNeeded();

            if (applyOnAwake)
                ApplyPlayerRigForCurrentMode();
        }

        private void Start()
        {
            if (!reapplyOnStart)
                return;

            EnsurePortingInitialized();
            ApplyPlayerRigForCurrentMode();
        }

        [ContextMenu("Apply Player Rig For Current Mode")]
        public void ApplyPlayerRigForCurrentMode()
        {
            AutoResolveRigRootsIfNeeded();

            AppMode mode = ResolveMode();
            bool pcActive = mode == AppMode.PC;
            bool vrActive = mode == AppMode.XR;

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

            if (logAppliedState)
            {
                Debug.Log(
                    $"[WasteCollectionPlayerRigController] AppMode={mode} → PC roots active={pcActive} ({pcPlayerRoots.Count}), " +
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

            if (ResolveMode() != AppMode.PC)
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

        private AppMode ResolveMode()
        {
            if (FirePlatformRuntime.IsSourceInitialized)
                return FirePlatformRuntime.CurrentMode;

            if (portingVariable != null)
                return portingVariable.CurrentValue;

            return fallbackMode;
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

        private void ResolvePortingVariable()
        {
#if UNITY_EDITOR
            portingVariable =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(PortingVariablePath);
#endif
        }
    }
}
