using System.Reflection;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Resolves the active XR headset <see cref="Camera"/> (XROrigin.Camera), not a stale scene reference.
    /// </summary>
    internal static class WasteVrHeadCameraResolver
    {
        private static readonly System.Type XrOriginType =
            System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");

        private static PropertyInfo s_cameraProperty;

        public static bool TryGetHeadCamera(Transform preferredRigRoot, out Camera camera)
        {
            camera = null;

            if (preferredRigRoot != null && TryGetCameraFromRig(preferredRigRoot, out camera))
                return true;

            if (TryFindActiveMainCameraInLoadedScenes(out camera))
                return true;

            if (XrOriginType == null)
                return false;

            System.Array origins = Resources.FindObjectsOfTypeAll(XrOriginType);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins.GetValue(i) is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid() || !go.activeInHierarchy)
                    continue;

                if (TryGetCameraFromOriginComponent(origin, out Camera candidate)
                    && IsBetterHeadCamera(candidate, camera))
                {
                    camera = candidate;
                }
            }

            return camera != null;
        }

        private static bool TryGetCameraFromRig(Transform rigRoot, out Camera camera)
        {
            camera = null;
            if (rigRoot == null)
                return false;

            if (XrOriginType != null
                && rigRoot.TryGetComponent(XrOriginType, out Component originOnRoot)
                && TryGetCameraFromOriginComponent(originOnRoot, out camera))
            {
                return true;
            }

            Component inParent = rigRoot.GetComponentInParent(XrOriginType);
            if (TryGetCameraFromOriginComponent(inParent, out camera))
                return true;

            Component inChild = rigRoot.GetComponentInChildren(XrOriginType, true);
            if (TryGetCameraFromOriginComponent(inChild, out camera))
                return true;

            return TryFindMainCameraInHierarchy(rigRoot, out camera);
        }

        private static bool TryFindMainCameraInHierarchy(Transform root, out Camera camera)
        {
            camera = null;
            if (root == null)
                return false;

            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled)
                    continue;

                if (cam.CompareTag("MainCamera"))
                {
                    camera = cam;
                    return true;
                }

                if (IsBetterHeadCamera(cam, camera))
                    camera = cam;
            }

            return camera != null;
        }

        private static bool TryFindActiveMainCameraInLoadedScenes(out Camera camera)
        {
            camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled && camera.gameObject.scene.IsValid())
                return true;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            camera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled || !cam.gameObject.scene.IsValid())
                    continue;

                if (cam.CompareTag("MainCamera"))
                {
                    camera = cam;
                    return true;
                }

                if (IsBetterHeadCamera(cam, camera))
                    camera = cam;
            }

            return camera != null;
        }

        private static bool TryGetCameraFromOriginComponent(Component origin, out Camera camera)
        {
            camera = null;
            if (origin == null || !origin.gameObject.activeInHierarchy)
                return false;

            s_cameraProperty ??= XrOriginType?.GetProperty(
                "Camera",
                BindingFlags.Instance | BindingFlags.Public);

            if (s_cameraProperty != null
                && s_cameraProperty.GetValue(origin) is Camera originCamera
                && originCamera.isActiveAndEnabled)
            {
                camera = originCamera;
                return true;
            }

            Camera childMain = origin.GetComponentInChildren<Camera>(true);
            if (childMain != null && childMain.isActiveAndEnabled)
            {
                camera = childMain;
                return true;
            }

            return false;
        }

        private static bool IsBetterHeadCamera(Camera candidate, Camera current)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
                return false;

            if (current == null)
                return true;

            if (candidate.CompareTag("MainCamera") && !current.CompareTag("MainCamera"))
                return true;

            return candidate.depth > current.depth;
        }
    }
}
