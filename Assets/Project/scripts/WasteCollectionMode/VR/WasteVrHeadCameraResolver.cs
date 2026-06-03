using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Resolves the active XR headset <see cref="Camera"/> (XROrigin.Camera), not a stale scene reference.
    /// Handles duplicate rigs such as "XR Origin (XR Rig)" and "XR Origin (XR Rig) (1)" after scene reload.
    /// </summary>
    public static class WasteVrHeadCameraResolver
    {
        private static readonly Type XrOriginType =
            Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");

        private static PropertyInfo s_cameraProperty;

        public static bool TryGetHeadCamera(Transform preferredRigRoot, out Camera camera)
        {
            camera = null;

            if (IsUsableRigRoot(preferredRigRoot) && TryGetCameraFromRig(preferredRigRoot, out camera))
                return true;

            if (TryGetBestHeadCamera(out camera, out _))
                return true;

            return false;
        }

        public static bool TryGetBestActiveXrRig(out Transform rigRoot)
        {
            rigRoot = null;
            return TryGetBestHeadCamera(out _, out rigRoot) && rigRoot != null;
        }

        public static bool IsUsableRigRoot(Transform rigRoot)
        {
            if (rigRoot == null || rigRoot.gameObject == null)
                return false;

            if (!rigRoot.gameObject.scene.IsValid())
                return false;

            return TryGetCameraFromRig(rigRoot, out Camera camera) && IsLikelyHeadCamera(camera);
        }

        private static bool TryGetBestHeadCamera(out Camera camera, out Transform rigRoot)
        {
            camera = null;
            rigRoot = null;
            int bestScore = int.MinValue;

            if (TryFindActiveMainCameraInLoadedScenes(out camera))
            {
                rigRoot = FindXrRigRootForCamera(camera);
                bestScore = ScoreRigCandidate(rigRoot, camera);
            }

            if (XrOriginType == null)
                return camera != null;

            Array origins = Resources.FindObjectsOfTypeAll(XrOriginType);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins.GetValue(i) is not Component origin || origin == null)
                    continue;

                Transform candidateRoot = origin.transform;
                if (!TryGetCameraFromRig(candidateRoot, out Camera candidateCamera)
                    || !IsLikelyHeadCamera(candidateCamera))
                {
                    continue;
                }

                int score = ScoreRigCandidate(candidateRoot, candidateCamera);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                camera = candidateCamera;
                rigRoot = candidateRoot;
            }

            return camera != null;
        }

        private static int ScoreRigCandidate(Transform rigRoot, Camera camera)
        {
            if (camera == null)
                return int.MinValue;

            int score = 0;

            if (rigRoot != null && rigRoot.gameObject.activeInHierarchy)
                score += 40;

            if (camera.gameObject.activeInHierarchy && camera.isActiveAndEnabled)
                score += 40;

            if (camera.CompareTag("MainCamera"))
                score += 25;

            if (rigRoot != null)
            {
                string rigName = rigRoot.name;
                if (rigName.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0
                    || rigName.IndexOf("XR Rig", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 15;
                }
            }

            string cameraName = camera.gameObject.name;
            if (cameraName.IndexOf("Main Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 10;

            Scene scene = camera.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                score += 10;
                string sceneName = scene.name;
                if (sceneName.IndexOf("Bootstrapper", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(sceneName, "WasteLogin", StringComparison.Ordinal))
                {
                    score -= 80;
                }
                else if (sceneName.IndexOf("FireModule", StringComparison.OrdinalIgnoreCase) >= 0
                         || sceneName.IndexOf("Office", StringComparison.OrdinalIgnoreCase) >= 0
                         || sceneName.IndexOf("Waste", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 30;
                }
            }

            score += Mathf.RoundToInt(camera.depth * 10f);
            return score;
        }

        private static Transform FindXrRigRootForCamera(Camera camera)
        {
            if (camera == null)
                return null;

            if (XrOriginType != null)
            {
                Component origin = camera.GetComponentInParent(XrOriginType);
                if (origin != null)
                    return origin.transform;
            }

            Transform walk = camera.transform;
            while (walk != null)
            {
                string name = walk.name;
                if (name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("XR Rig", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return walk;
                }

                walk = walk.parent;
            }

            return camera.transform.root;
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
                if (!IsLikelyHeadCamera(cam))
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
            if (IsLikelyHeadCamera(camera) && camera.gameObject.scene.IsValid())
                return true;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            camera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (!IsLikelyHeadCamera(cam) || !cam.gameObject.scene.IsValid())
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
            if (origin == null)
                return false;

            s_cameraProperty ??= XrOriginType?.GetProperty(
                "Camera",
                BindingFlags.Instance | BindingFlags.Public);

            if (s_cameraProperty != null
                && s_cameraProperty.GetValue(origin) is Camera originCamera
                && IsLikelyHeadCamera(originCamera))
            {
                camera = originCamera;
                return true;
            }

            Camera[] cameras = origin.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (!IsLikelyHeadCamera(cam))
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

        private static bool IsLikelyHeadCamera(Camera camera)
        {
            if (camera == null || !camera.isActiveAndEnabled)
                return false;

            if (!camera.gameObject.activeInHierarchy)
                return false;

            if (camera.cullingMask == 0)
                return false;

            string objectName = camera.gameObject.name;
            if (objectName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Transition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            Transform root = camera.transform.root;
            if (root != null)
            {
                string rootName = root.name;
                if (rootName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0
                    || rootName.IndexOf("Transition", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBetterHeadCamera(Camera candidate, Camera current)
        {
            if (!IsLikelyHeadCamera(candidate))
                return false;

            if (current == null)
                return true;

            int candidateScore = ScoreRigCandidate(FindXrRigRootForCamera(candidate), candidate);
            int currentScore = ScoreRigCandidate(FindXrRigRootForCamera(current), current);
            return candidateScore > currentScore;
        }
    }
}
