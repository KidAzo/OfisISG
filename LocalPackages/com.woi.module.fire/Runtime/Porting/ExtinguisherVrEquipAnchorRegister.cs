using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// [DEPRECATED] Eski ray-to-point VR tüp taşıma sistemi devre dışı bırakıldı.
    /// Lütfen yeni sistem için VRHandExtinguisherGrabber kullanın.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Porting/Extinguisher VR Equip Anchor Register (Deprecated)")]
    public sealed class ExtinguisherVrEquipAnchorRegister : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Boş bırakılırsa bu component'in olduğu transform kullanılır.")]
        private Transform equipAnchor;

        private void Awake()
        {
            // İptal edildi: Eski sistemin yeni grab ile çakışmasını önlemek için devre dışı bırakıldı.
        }

        private void OnEnable()
        {
            // FireVrGameplayEquipAnchor.Register(this, equipAnchor);
        }

        private void OnDisable()
        {
            // FireVrGameplayEquipAnchor.Unregister(this);
        }
    }
}
