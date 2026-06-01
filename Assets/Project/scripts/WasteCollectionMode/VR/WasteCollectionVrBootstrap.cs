using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR entry: skip login session defaults, disable PC mouse picker, enable world UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteCollectionVrBootstrap : MonoBehaviour
    {
        [Header("VR identity (no login scene)")]
        [SerializeField] private string defaultUserName = "VR Trainee";
        [SerializeField] private string defaultUserId = "vr-trainee";
        [SerializeField] private string defaultLanguageCode = "tr";

        [Header("PC systems to disable in VR")]
        [SerializeField] private SelectionSystemManager selectionSystemManager;

        private void Awake()
        {
            if (!WasteCollectionPlatform.IsVR)
            {
                enabled = false;
                return;
            }

            EnsureVrSession();
            DisablePcSelection();
        }

        private void EnsureVrSession()
        {
            if (WasteLoginSession.IsSet)
                return;

            WasteLoginSession.Set(defaultUserName, defaultUserId, defaultLanguageCode);
        }

        private void DisablePcSelection()
        {
            if (selectionSystemManager == null)
                selectionSystemManager = FindFirstObjectByType<SelectionSystemManager>();

            if (selectionSystemManager != null)
                selectionSystemManager.enabled = false;
        }
    }
}
