using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Training
{
    /// <summary>
    /// Aynı <see cref="FireSource"/> GameObject’ında veya altında: VR eğitim world kartları
    /// (proximity, yanlış tüp, yangın söndü) için sabit dünya konumu ve isteğe bağlı ölçek.
    /// Ölçek: atanan world popup transformunun (veya bu objenin) <c>lossyScale</c> eksenlerinin mutlak maksimumu,
    /// <see cref="ExtinguisherHoverVrWorldPopupHost"/> içindeki <c>worldDocumentScale</c> ile çarpılır; (1,1,1) = varsayılan boyut.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Source World Popup Anchor")]
    public sealed class FireSourceTrainingWorldPopupAnchor : MonoBehaviour
    {
        [Tooltip("Doluysa kart bu transformun world pozisyonundan çıkar; boşsa bu bileşenin bulunduğu objenin pozisyonu.")]
        [SerializeField]
        Transform _worldPopupPoint;

        [Tooltip("Açıksa VR kart ölçeği yukarıdaki transformun lossyScale (|x|,|y|,|z| maks) ile host worldDocumentScale çarpılır; kapalıysa yalnızca konum anchor’dan gelir.")]
        [SerializeField]
        private bool _applyLossyScaleToVrPopup = true;

        /// <summary>World uzayında anchor noktası (ek ofsetler <see cref="TrainingVrFireWorldCardPlacement"/> tarafından eklenir).</summary>
        public bool TryGetWorldPopupPosition(out Vector3 worldPosition) =>
            TryGetWorldPopupPlacement(out worldPosition, out _);

        /// <summary>
        /// World pozisyon ve <see cref="ExtinguisherHoverVrWorldPopupHost"/> worldDocumentScale için çarpan (1 = değiştirme).
        /// </summary>
        public bool TryGetWorldPopupPlacement(out Vector3 worldPosition, out float? worldDocumentScaleMultiplier)
        {
            worldPosition = default;
            worldDocumentScaleMultiplier = null;

            Transform t = _worldPopupPoint != null ? _worldPopupPoint : transform;
            if (t == null)
                return false;

            worldPosition = t.position;

            if (_applyLossyScaleToVrPopup)
                worldDocumentScaleMultiplier = ComputeUniformScaleMultiplier(t);

            return true;
        }

        /// <summary>Host <c>worldDocumentScale</c> ile çarpılacak pozitif çarpan.</summary>
        public static float ComputeUniformScaleMultiplier(Transform t)
        {
            if (t == null)
                return 1f;

            Vector3 s = t.lossyScale;
            float m = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            return Mathf.Clamp(m, 0.01f, 50f);
        }
    }
}
