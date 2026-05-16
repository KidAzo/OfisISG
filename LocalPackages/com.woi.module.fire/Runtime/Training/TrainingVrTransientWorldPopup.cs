using System.Collections;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.UI.Announcements;

namespace Woi.Training
{
    /// <summary>
    /// Kısa süreli eğitim bildirimleri (yanlış tüp, yangın söndü vb.) için XR’da world UI Toolkit kartı;
    /// PC’de kullanılmaz — çağıran <see cref="IPopupService"/> yoluna düşer.
    /// </summary>
    public static class TrainingVrTransientWorldPopup
    {
        public static bool TryBegin(
            MonoBehaviour owner,
            string title,
            string message,
            float visibleSeconds,
            float liftMetersFromFeet,
            float separationMeters,
            float towardCameraMeters,
            ref Coroutine hideRoutine)
        {
            if (owner == null || !TrainingPlayerAnchorResolver.IsTrainingVrMode())
                return false;

            if (!ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vr) || vr == null)
                return false;

            if (!TrainingPlayerAnchorResolver.TryComputeVrTrainingPopupAnchor(
                    out Vector3 anchor,
                    out Vector3 normal,
                    liftMetersFromFeet))
                return false;

            if (hideRoutine != null)
            {
                owner.StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            vr.ShowAt(
                anchor,
                normal,
                title ?? string.Empty,
                message ?? string.Empty,
                separationMeters,
                towardCameraMeters);

            hideRoutine = owner.StartCoroutine(HideAfterSeconds(vr, Mathf.Max(0.05f, visibleSeconds)));
            return true;
        }

        /// <summary>
        /// Yangın proximity kartı ile aynı mantık: varsa <see cref="FireSourceTrainingWorldPopupAnchor"/>,
        /// yoksa <see cref="TrainingVrFireWorldCardPlacement"/> (collider / rig yanı) (normal = dünya yukarısı).
        /// </summary>
        public static bool TryBeginAtFire(
            MonoBehaviour owner,
            FireSource fire,
            string title,
            string message,
            float visibleSeconds,
            in TrainingVrFireWorldCardPlacement.Layout fireLayout,
            float separationAlongUp,
            float towardCameraMeters,
            VrWorldTrainingCardTone cardTone,
            ref Coroutine hideRoutine)
        {
            if (owner == null || !TrainingPlayerAnchorResolver.IsTrainingVrMode())
                return false;

            if (!fireLayout.PlaceBesidePlayerRig && fire == null)
                return false;

            if (!TrainingVrFireWorldCardPlacement.TryComputeAnchor(
                    fire,
                    in fireLayout,
                    out Vector3 anchor,
                    out float? worldDocumentScaleMultiplier))
                return false;

            if (!ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vr) || vr == null)
                return false;

            if (hideRoutine != null)
            {
                owner.StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            float scaleMul = worldDocumentScaleMultiplier ?? float.NaN;
            vr.ShowAt(
                anchor,
                Vector3.up,
                title ?? string.Empty,
                message ?? string.Empty,
                separationAlongUp,
                towardCameraMeters,
                cardTone,
                scaleMul);

            hideRoutine = owner.StartCoroutine(HideAfterSeconds(vr, Mathf.Max(0.05f, visibleSeconds)));
            return true;
        }

        public static void CancelHideRoutine(MonoBehaviour owner, ref Coroutine hideRoutine)
        {
            if (owner != null && hideRoutine != null)
            {
                owner.StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
        }

        static IEnumerator HideAfterSeconds(ExtinguisherHoverVrWorldPopupHost vr, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (vr != null)
                vr.Hide();
        }
    }
}
