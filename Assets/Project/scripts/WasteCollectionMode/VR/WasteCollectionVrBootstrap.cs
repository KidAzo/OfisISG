using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR entry: skip login session defaults. Selection uses <see cref="SelectionSystemManager"/> (trigger + right-controller ray).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteCollectionVrBootstrap : MonoBehaviour
    {
        [Header("VR identity (no login scene)")]
        [SerializeField] private string defaultUserName = "VR Trainee";
        [SerializeField] private string defaultUserId = "vr-trainee";
        [SerializeField] private string defaultLanguageCode = "tr";

        private void Awake()
        {
            if (!WasteCollectionPlatform.IsVR)
            {
                enabled = false;
                return;
            }

            EnsureVrSession();
        }

        private void EnsureVrSession()
        {
            if (WasteLoginSession.IsSet)
                return;

            WasteLoginSession.Set(defaultUserName, defaultUserId, defaultLanguageCode);
        }
    }
}
