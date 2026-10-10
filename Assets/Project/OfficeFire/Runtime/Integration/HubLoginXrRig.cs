using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Login-only XR rig gate. The rig stays in the login scene and is destroyed with it.
    /// PC leaves the rig inactive and keeps the existing camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubLoginXrRig : MonoBehaviour
    {
        [SerializeField] GameObject xrOriginRoot;
        [SerializeField] GameObject pcCameraRoot;
        [SerializeField] GameObject screenSpaceCanvasRoot;

        void Awake()
        {
            StandalonePlatform.ApplyIfNoHubOverride();
            bool vr = StandalonePlatform.IsVrActive;
            if (xrOriginRoot != null)
                xrOriginRoot.SetActive(vr);

            if (!vr)
                return;

            if (pcCameraRoot != null)
                pcCameraRoot.SetActive(false);

            if (screenSpaceCanvasRoot != null)
                screenSpaceCanvasRoot.SetActive(false);
        }
    }
}
