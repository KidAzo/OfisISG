using Obvious.Soap;
using FireExtinguisher.Core;
using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Announcements;
using Woi.UI.Popups;
using Woi.UI.Popups.Localization;

namespace Woi.Training
{
    /// <summary>
    /// <see cref="ScriptableEventNoParam.OnRaised"/> (yanlış tüp SOAP) tetiklenince
    /// XR eğitim modunda <see cref="TrainingVrTransientWorldPopup"/> ile world kartı; aksi halde
    /// <see cref="IPopupService"/> üzerinden kısa, kırmızı tonlu uyarı kartı.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Extinguisher Wrong Tube Popup Bridge")]
    public sealed class ExtinguisherWrongTubePopupBridge : MonoBehaviour
    {
        [Header("SOAP")]
        [Tooltip("ExtinguisherSprayTubeSoapBridge ile aynı yanlış tüp event asset’i.")]
        [SerializeField]
        private ScriptableEventNoParam _onSprayWrongTubeForFire;

        [Header("Popup")]
        [Tooltip("Doluysa bu asset kullanılır; Content Variants [0] içinde dil satırları dolu olmalı ve Type = Error (kırmızı kart) önerilir. Boşsa aşağıdaki TR/EN alanları + ShowText kullanılır.")]
        [SerializeField]
        private PopupDefinition _popupDefinition;

        [Tooltip("Satır içi metin yolu için: Error = kırmızı USS. Özel PopupDefinition kullanıyorsan asset’teki Type da Error olmalı.")]
        [SerializeField]
        private PopupType _popupType = PopupType.Error;

        [Tooltip("Otomatik kapanmadan önce ekranda kalma süresi (saniye).")]
        [SerializeField]
        [Min(0.5f)]
        private float _visibleSeconds = 2.5f;

        [Tooltip("Yalnızca _popupDefinition boşken kullanılır (Türkçe metin).")]
        [SerializeField]
        private string _titleTr = "Yanlış tüp";

        [SerializeField]
        private string _messageTr = "Bu yangın sınıfı için uygun olmayan bir söndürücü kullanıyorsunuz.";

        [SerializeField]
        private string _titleEn = "Wrong extinguisher";

        [SerializeField]
        private string _messageEn = "You are using an extinguisher that is not suitable for this fire class.";

        [Header("VR world kart — yangın üstü (proximity ile aynı mantık)")]
        [Tooltip("En büyük hacimli (non-trigger) child collider merkezini anchor olarak kullan.")]
        [SerializeField]
        private bool _vrFireUseColliderCenter = true;

        [SerializeField, Min(0f)]
        private float _vrFireLiftFromColliderCenter = 0.2f;

        [SerializeField, Min(0f)]
        private float _vrFireHeightAboveRoot = 1.35f;

        [SerializeField, Min(0f)]
        private float _vrFireExtraWorldYOffset = 0.4f;

        [Tooltip("Normal dünya yukarısı: anchor’dan ek yükseltme (m).")]
        [SerializeField, Min(0f)]
        private float _vrFireSeparationAlongUp = 0.08f;

        [SerializeField, Min(0f)]
        private float _vrFireTowardViewerExtra = 0f;

        [Tooltip("Yangın anchor’u (collider/kök + Y) hesabından sonra dünya uzayında ek X/Y/Z (m). Yanlış tüp kartını kaydırmak için.")]
        [SerializeField]
        private Vector3 _vrFireAdditionalWorldOffset;

        [Tooltip("Açıksa kart yangın yerine rig yerel uzayında: X=sağ, Y=yukarı, Z=ileri (m).")]
        [SerializeField]
        private bool _vrFirePlaceBesidePlayerRig;

        [SerializeField]
        private Vector3 _vrFireBesidePlayerLocalOffsetMeters = new Vector3(0.8f, 1.45f, 0.3f);

        Coroutine _vrHideRoutine;

        private void OnEnable()
        {
            if (_onSprayWrongTubeForFire == null)
                return;

            _onSprayWrongTubeForFire.OnRaised += HandleWrongTube;
        }

        private void OnDisable()
        {
            TrainingVrTransientWorldPopup.CancelHideRoutine(this, ref _vrHideRoutine);

            if (_onSprayWrongTubeForFire != null)
                _onSprayWrongTubeForFire.OnRaised -= HandleWrongTube;
        }

        private void HandleWrongTube()
        {
            ResolveInlineStrings(out string title, out string message);

            FireSource fire = TrainingVrTransientSoapFireContext.LastWrongTubeFire;
            var layout = new TrainingVrFireWorldCardPlacement.Layout(
                _vrFireUseColliderCenter,
                _vrFireLiftFromColliderCenter,
                _vrFireHeightAboveRoot,
                _vrFireExtraWorldYOffset,
                _vrFireAdditionalWorldOffset,
                _vrFirePlaceBesidePlayerRig,
                _vrFireBesidePlayerLocalOffsetMeters);

            if (TrainingVrTransientWorldPopup.TryBeginAtFire(
                    this,
                    fire,
                    title,
                    message,
                    _visibleSeconds,
                    in layout,
                    _vrFireSeparationAlongUp,
                    _vrFireTowardViewerExtra,
                    VrWorldTrainingCardTone.TrainingSoapError,
                    ref _vrHideRoutine))
                return;

            if (!ServiceLocator.TryGet<IPopupService>(out var popups) || popups == null)
                return;

            if (_popupDefinition != null)
            {
                popups.Replace(_popupDefinition, _visibleSeconds, blockInputOverride: false);
                return;
            }

            popups.ShowText(title, message, _popupType, _visibleSeconds);
        }

        /// <summary>
        /// Metni burada seçiyoruz: çalışma zamanı <see cref="PopupDefinition"/> + çok satırlı variant
        /// bazen Unity’de boş çözümlenebiliyor; tek satırlı <see cref="IPopupService.ShowText"/> güvenilir.
        /// </summary>
        private void ResolveInlineStrings(out string title, out string message)
        {
            bool turkish = PreferTurkish();
            title = turkish ? _titleTr : _titleEn;
            message = turkish ? _messageTr : _messageEn;
        }

        private static bool PreferTurkish()
        {
            string code = null;
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService iloc) && iloc != null)
                code = iloc.CurrentLanguage;
            else if (LocalizationService.Instance != null)
                code = LocalizationService.Instance.CurrentLanguage;

            if (string.IsNullOrWhiteSpace(code))
                return true;

            code = code.Trim().ToLowerInvariant();
            if (code == LocalizationService.Turkish || code.StartsWith("tr", System.StringComparison.Ordinal))
                return true;

            return false;
        }
    }
}
