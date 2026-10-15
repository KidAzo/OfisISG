using System.Collections;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Login-only XR rig gate. The rig stays in the login scene and is destroyed with it.
    /// PC leaves the rig inactive and keeps the existing camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class HubLoginXrRig : MonoBehaviour
    {
        [SerializeField] GameObject xrOriginRoot;
        [SerializeField] GameObject pcCameraRoot;
        [SerializeField] GameObject screenSpaceCanvasRoot;

        static bool _enterLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTrace()
        {
            _enterLogged = false;
        }

        void Awake()
        {
            if (!_enterLogged)
            {
                _enterLogged = true;
                Debug.Log("[FireTrace] FireLoginPlatformSetup ENTER");
            }

            TryAutoWireReferences();
            StandalonePlatform.ApplyIfNoHubOverride();
            bool vr = StandalonePlatform.IsVrActive;

#if UNITY_EDITOR
            if (vr)
                StandalonePlatform.SyncEditorXrLoader(true);
#endif

            if (xrOriginRoot != null)
                xrOriginRoot.SetActive(vr);

            if (!vr)
            {
                EnsurePresentation().Initialize(xrOriginRoot, false);
                return;
            }

            if (pcCameraRoot != null)
                pcCameraRoot.SetActive(false);

            if (screenSpaceCanvasRoot != null)
                screenSpaceCanvasRoot.SetActive(false);

            EnsureXrCamerasEnabled();
            EnsurePresentation().Initialize(xrOriginRoot, true);
        }

        LoginVrPresentation EnsurePresentation()
        {
            LoginVrPresentation presentation = GetComponent<LoginVrPresentation>();
            return presentation != null ? presentation : gameObject.AddComponent<LoginVrPresentation>();
        }

        void Start()
        {
            if (!StandalonePlatform.IsVrActive)
                return;

            StartCoroutine(WarmEditorLinkPresentation());
        }

        IEnumerator WarmEditorLinkPresentation()
        {
#if !UNITY_EDITOR
            yield break;
#else
            int[] delays = { 1, 5, 15, 30, 60 };
            for (int i = 0; i < delays.Length; i++)
            {
                for (int f = 0; f < (i == 0 ? delays[i] : delays[i] - delays[i - 1]); f++)
                    yield return null;

                if (!StandalonePlatform.IsVrActive)
                    yield break;

                StandalonePlatform.SyncEditorXrLoader(true);
                if (xrOriginRoot != null && !xrOriginRoot.activeSelf)
                    xrOriginRoot.SetActive(true);

                EnsureXrCamerasEnabled();
            }
#endif
        }

        void EnsureXrCamerasEnabled()
        {
            if (xrOriginRoot == null)
                return;

            Camera[] cameras = xrOriginRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                    continue;

                if (!camera.gameObject.activeSelf)
                    camera.gameObject.SetActive(true);

                camera.enabled = true;
            }
        }

        void TryAutoWireReferences()
        {
            if (xrOriginRoot == null)
            {
                GameObject origin = FindXrOriginObject();
                if (origin != null)
                    xrOriginRoot = origin;
            }

            if (pcCameraRoot == null)
            {
                Camera[] cameras = Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || !string.Equals(camera.gameObject.name, "Main Camera"))
                        continue;

                    if (xrOriginRoot != null && camera.transform.IsChildOf(xrOriginRoot.transform))
                        continue;

                    pcCameraRoot = camera.gameObject;
                    break;
                }
            }
        }

        static GameObject FindXrOriginObject()
        {
            System.Type originType = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return null;

            Object[] found = Resources.FindObjectsOfTypeAll(originType);
            GameObject best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is not Component origin || origin == null)
                    continue;

                GameObject candidate = origin.gameObject;
                if (!candidate.scene.IsValid())
                    continue;

                int score = candidate.activeInHierarchy ? 10 : 0;
                string name = candidate.name;
                if (name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("XR Rig", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 20;
                }

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }
    }
}
