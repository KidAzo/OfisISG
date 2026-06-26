using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Disables XRI jump while Office Fire VR pin pull uses Primary (A / X).
    /// JumpProvider re-enables its Jump action in OnEnable, so we disable that action every frame too.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class OfficeFireVrJumpSuppressor : MonoBehaviour
    {
        const string JumpProviderTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider";

        const string InputActionManagerTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager";

        static readonly FieldInfo InputActionManagerAssetsField = ResolveInputActionManagerAssetsField();

        readonly List<InputActionAsset> _cachedAssets = new();
        bool _loggedMissingJumpAction;

        static FieldInfo ResolveInputActionManagerAssetsField()
        {
            Type managerType = Type.GetType(
                InputActionManagerTypeName + ", Unity.XR.Interaction.Toolkit");
            return managerType?.GetField(
                "m_ActionAssets",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        void OnEnable()
        {
            CacheInputActionAssets();
            SuppressJump();
        }

        void Start() => SuppressJump();

        void Update()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            SuppressJump();
        }

        void LateUpdate()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            SuppressJump();
        }

        public void SuppressJumpProviders() => SuppressJump();

        void SuppressJump()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            SuppressJumpProviderComponents();
            SuppressXriJumpInputActions();
        }

        void SuppressJumpProviderComponents()
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                if (!string.Equals(type.FullName, JumpProviderTypeName, StringComparison.Ordinal))
                    continue;

                if (behaviour.enabled)
                    behaviour.enabled = false;

                GameObject jumpObject = behaviour.gameObject;
                if (jumpObject.activeSelf)
                    jumpObject.SetActive(false);
            }
        }

        void SuppressXriJumpInputActions()
        {
            CacheInputActionAssets();

            int disabledActions = 0;
            for (int i = 0; i < _cachedAssets.Count; i++)
                disabledActions += DisableJumpActionsInAsset(_cachedAssets[i]);

            if (disabledActions == 0 && !_loggedMissingJumpAction)
            {
                _loggedMissingJumpAction = true;
                Debug.LogWarning(
                    "[OfficeFireVrJumpSuppressor] XRI Jump InputAction not found on XR rig — A/X may still jump.",
                    this);
            }
        }

        void CacheInputActionAssets()
        {
            _cachedAssets.Clear();

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (!string.Equals(behaviour.GetType().FullName, InputActionManagerTypeName, StringComparison.Ordinal))
                    continue;

                if (InputActionManagerAssetsField?.GetValue(behaviour) is IList<InputActionAsset> reflectedAssets)
                {
                    for (int a = 0; a < reflectedAssets.Count; a++)
                        TryAddAsset(reflectedAssets[a]);
                }
            }

            if (_cachedAssets.Count > 0)
                return;

            InputActionAsset[] allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            for (int i = 0; i < allAssets.Length; i++)
            {
                InputActionAsset asset = allAssets[i];
                if (asset == null || !asset.name.Contains("XRI", StringComparison.OrdinalIgnoreCase))
                    continue;

                TryAddAsset(asset);
            }
        }

        void TryAddAsset(InputActionAsset asset)
        {
            if (asset == null || _cachedAssets.Contains(asset))
                return;

            _cachedAssets.Add(asset);
        }

        static int DisableJumpActionsInAsset(InputActionAsset asset)
        {
            if (asset == null)
                return 0;

            int disabled = 0;
            foreach (InputActionMap map in asset.actionMaps)
            {
                InputAction jump = map.FindAction("Jump", false);
                if (jump == null)
                    continue;

                if (jump.enabled)
                {
                    jump.Disable();
                    disabled++;
                }
            }

            return disabled;
        }
    }
}
