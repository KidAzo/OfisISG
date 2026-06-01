using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Temporarily disables XR locomotion / teleport while waste UI is open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrLocomotionGate : MonoBehaviour
    {
        [SerializeField] private Transform xrRigRoot;
        [SerializeField] private string playerTag = "Player";

        private readonly List<Behaviour> disabledBehaviours = new();
        private readonly List<GameObject> deactivatedRoots = new();
        private bool locomotionDisabled;

        public void SetLocomotionEnabled(bool enabled)
        {
            if (enabled)
                RestoreLocomotion();
            else
                DisableLocomotion();
        }

        private void DisableLocomotion()
        {
            if (locomotionDisabled)
                return;

            disabledBehaviours.Clear();
            deactivatedRoots.Clear();

            Transform rig = ResolveRigRoot();
            if (rig == null)
                return;

            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child == null)
                    continue;

                if (string.Equals(child.name, "Locomotion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.name, "Teleportation", StringComparison.OrdinalIgnoreCase))
                {
                    if (child.gameObject.activeSelf)
                    {
                        deactivatedRoots.Add(child.gameObject);
                        child.gameObject.SetActive(false);
                    }
                }
            }

            foreach (MonoBehaviour mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.enabled)
                    continue;

                string fullName = mb.GetType().FullName ?? string.Empty;
                if (ShouldDisableLocomotionBehaviour(fullName))
                {
                    disabledBehaviours.Add(mb);
                    mb.enabled = false;
                }
            }

            locomotionDisabled = true;
        }

        private void RestoreLocomotion()
        {
            if (!locomotionDisabled)
                return;

            for (int i = 0; i < disabledBehaviours.Count; i++)
            {
                Behaviour behaviour = disabledBehaviours[i];
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            for (int i = 0; i < deactivatedRoots.Count; i++)
            {
                GameObject root = deactivatedRoots[i];
                if (root != null)
                    root.SetActive(true);
            }

            disabledBehaviours.Clear();
            deactivatedRoots.Clear();
            locomotionDisabled = false;
        }

        private static bool ShouldDisableLocomotionBehaviour(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return false;

            if (fullName == "UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager")
                return true;

            if (!fullName.StartsWith("UnityEngine.XR.Interaction.Toolkit.Locomotion.", StringComparison.Ordinal))
                return false;

            return fullName.Contains("Teleportation", StringComparison.Ordinal) ||
                   fullName.Contains("MoveProvider", StringComparison.Ordinal) ||
                   (fullName.Contains("Turn", StringComparison.Ordinal) && fullName.Contains("Provider", StringComparison.Ordinal)) ||
                   fullName.EndsWith(".LocomotionSystem", StringComparison.Ordinal);
        }

        private Transform ResolveRigRoot()
        {
            if (xrRigRoot != null)
                return xrRigRoot;

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                    return tagged.transform;
            }

            return null;
        }
    }
}
