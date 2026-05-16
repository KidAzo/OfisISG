using System;
using UnityEngine;
using WOI.Modules.SDK;
using Woi.Player;

namespace Woi.Training
{
    /// <summary>
    /// Eğitim SOAP / popup akışları için oyuncu kökü (PC) veya XR rig kökü (XR) dünya konumu.
    /// Önce <see cref="IXRPlayerService"/> / <see cref="IPlayerService"/>; XR’da servis yoksa <c>XROrigin</c> sahne araması (SceneLoader ile aynı yöntem).
    /// </summary>
    public static class TrainingPlayerAnchorResolver
    {
        static Transform s_cachedXrRigTransform;

        public static bool IsTrainingVrMode()
        {
            if (ServiceLocator.TryGet<IFirePortingPlatformSource>(out var porting) && porting != null)
                return porting.CurrentMode == AppMode.XR;
            return FirePlatformRuntime.IsVR;
        }

        /// <summary>XR: rig kökü; PC: <see cref="IPlayerService.GetPlayerTransform"/>.</summary>
        public static bool TryGetAnchorWorldTransform(out Transform rigTransform)
        {
            rigTransform = null;

            if (IsTrainingVrMode())
            {
                if (ServiceLocator.TryGet<IXRPlayerService>(out var xr) && xr?.PlayerTransform != null)
                {
                    s_cachedXrRigTransform = xr.PlayerTransform;
                    rigTransform = xr.PlayerTransform;
                    return true;
                }

                if (s_cachedXrRigTransform != null)
                {
                    rigTransform = s_cachedXrRigTransform;
                    return true;
                }

                Transform found = TryFindXrOriginTransformInLoadedScenes();
                if (found != null)
                {
                    s_cachedXrRigTransform = found;
                    rigTransform = found;
                    return true;
                }

                return false;
            }

            if (ServiceLocator.TryGet<IPlayerService>(out var pc) && pc != null)
            {
                Transform t = pc.GetPlayerTransform();
                if (t != null)
                {
                    rigTransform = t;
                    return true;
                }
            }

            return false;
        }

        /// <summary>XR: rig kökü; PC: <see cref="IPlayerService.GetPlayerTransform"/>.</summary>
        public static bool TryGetAnchorWorldPosition(out Vector3 worldPosition)
        {
            if (!TryGetAnchorWorldTransform(out Transform t))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = t.position;
            return true;
        }

        /// <summary>XR: center-eye; PC: <see cref="IPlayerService.playerCamera"/>; yedek <see cref="Camera.main"/>.</summary>
        public static bool TryGetViewEyeWorldPosition(out Vector3 eyeWorld)
        {
            eyeWorld = default;

            if (IsTrainingVrMode())
            {
                if (ServiceLocator.TryGet<IXRPlayerService>(out var xr) && xr?.PlayerCamera != null)
                {
                    eyeWorld = xr.PlayerCamera.transform.position;
                    return true;
                }
            }
            else if (ServiceLocator.TryGet<IPlayerService>(out var pc) && pc != null && pc.playerCamera != null)
            {
                eyeWorld = pc.playerCamera.transform.position;
                return true;
            }

            if (Camera.main != null)
            {
                eyeWorld = Camera.main.transform.position;
                return true;
            }

            return TryGetAnchorWorldPosition(out eyeWorld);
        }

        /// <summary>Rig ayakları + yukarı lift; normal gözden karta doğru.</summary>
        public static bool TryComputeVrTrainingPopupAnchor(
            out Vector3 anchorWorld,
            out Vector3 normalTowardViewer,
            float liftMetersFromFeet)
        {
            anchorWorld = default;
            normalTowardViewer = Vector3.up;

            if (!TryGetAnchorWorldPosition(out Vector3 feet))
                return false;

            anchorWorld = feet + Vector3.up * Mathf.Max(0.1f, liftMetersFromFeet);

            if (!TryGetViewEyeWorldPosition(out Vector3 eye))
                return true;

            Vector3 toEye = eye - anchorWorld;
            if (toEye.sqrMagnitude > 1e-6f)
                normalTowardViewer = toEye.normalized;

            return true;
        }

        /// <summary>Porting veya sahne değişiminden sonra XR kök önbelleğini temizlemek için (isteğe bağlı).</summary>
        public static void ClearXrRigCache() => s_cachedXrRigTransform = null;

        static Transform TryFindXrOriginTransformInLoadedScenes()
        {
            Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (originType == null)
                return null;

            Array found = Resources.FindObjectsOfTypeAll(originType);
            for (int i = 0; i < found.Length; i++)
            {
                if (found.GetValue(i) is not Component origin || origin == null)
                    continue;

                GameObject go = origin.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                return origin.transform;
            }

            return null;
        }
    }
}
