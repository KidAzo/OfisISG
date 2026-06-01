using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Temporarily disables XR locomotion / teleport while waste UI is open.
    /// Uses behaviour.enabled only (never SetActive) to avoid activation stack conflicts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrLocomotionGate : MonoBehaviour
    {
        [SerializeField] private Transform xrRigRoot;
        [SerializeField] private string playerTag = "Player";

        private readonly List<Behaviour> disabledBehaviours = new();
        private bool locomotionDisabled;

        public Transform XrRigRoot => xrRigRoot;

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

            Transform rig = ResolveRigRoot();
            if (rig == null)
                return;

            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !IsLocomotionRootName(child.name))
                    continue;

                DisableBehavioursUnder(child);
            }

            foreach (MonoBehaviour mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.enabled)
                    continue;

                string fullName = mb.GetType().FullName ?? string.Empty;
                if (ShouldDisableLocomotionBehaviour(fullName) || IsLocomotionSystemBehaviour(mb))
                    DisableBehaviour(mb);
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

            disabledBehaviours.Clear();
            locomotionDisabled = false;
        }

        private void DisableBehavioursUnder(Transform root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                DisableBehaviour(behaviours[i]);
        }

        private void DisableBehaviour(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled || disabledBehaviours.Contains(behaviour))
                return;

            disabledBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }

        private static bool IsLocomotionRootName(string objectName)
        {
            return string.Equals(objectName, "Locomotion", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(objectName, "Teleportation", StringComparison.OrdinalIgnoreCase);
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

        private static bool IsLocomotionSystemBehaviour(MonoBehaviour behaviour)
        {
            if (behaviour == null)
                return false;

            System.Type type = behaviour.GetType();
            return type.Name == "LocomotionSystem"
                && type.Namespace != null
                && type.Namespace.StartsWith(
                    "UnityEngine.XR.Interaction.Toolkit.Locomotion",
                    StringComparison.Ordinal);
        }

        private Transform ResolveRigRoot()
        {
            if (xrRigRoot != null)
                return xrRigRoot;

            System.Type originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType != null)
            {
                Array found = Resources.FindObjectsOfTypeAll(originType);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found.GetValue(i) is Component origin && origin != null && origin.gameObject.scene.IsValid())
                        return origin.transform;
                }
            }

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
