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
    /// <see cref="FireSourceExtinguishedSoapBridge"/> ile aynı SOAP event’i dinler; yangın tamamen
    /// söndüğünde XR eğitim modunda world kartı, aksi halde yeşil tonlu kısa bildirim (yanlış tüp popup’ına paralel akış).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Fully Extinguished Popup Bridge")]
    public sealed class FireFullyExtinguishedPopupBridge : MonoBehaviour
    {
        [Header("SOAP")]
        [Tooltip("FireSourceExtinguishedSoapBridge’teki _onFireFullyExtinguished ile aynı asset.")]
        [SerializeField]
        private ScriptableEventNoParam _onFireFullyExtinguished;

        [Header("Popup")]
        [Tooltip("Doluysa bu PopupDefinition kullanılır (tr+en satırları, Type = Success önerilir). Boşsa aşağıdaki metinler.")]
        [SerializeField]
        private PopupDefinition _popupDefinition;

        [Tooltip("Satır içi metin için Success = yeşil USS.")]
        [SerializeField]
        private PopupType _popupType = PopupType.Success;

        [Tooltip("Otomatik kapanma süresi (saniye).")]
        [SerializeField]
        [Min(0.5f)]
        private float _visibleSeconds = 2.5f;

        [Tooltip("Yalnızca _popupDefinition boşken (başlık — Türkçe).")]
        [SerializeField]
        private string _titleTr = "Yangın söndürüldü";

        [SerializeField]
        private string _messageTr = "Yangın başarıyla söndürüldü.";

        [SerializeField]
        private string _titleEn = "Fire extinguished";

        [SerializeField]
        private string _messageEn = "The fire has been successfully extinguished.";

        [Header("VR world kart — yangın üstü (proximity ile aynı mantık)")]
        [SerializeField]
        private bool _vrFireUseColliderCenter = true;

        [SerializeField, Min(0f)]
        private float _vrFireLiftFromColliderCenter = 0.2f;

        [SerializeField, Min(0f)]
        private float _vrFireHeightAboveRoot = 1.35f;

        [SerializeField, Min(0f)]
        private float _vrFireExtraWorldYOffset = 0.4f;

        [SerializeField, Min(0f)]
        private float _vrFireSeparationAlongUp = 0.08f;

        [SerializeField, Min(0f)]
        private float _vrFireTowardViewerExtra = 0f;

        [Tooltip("Yangın anchor’undan sonra dünya uzayında ek X/Y/Z (m).")]
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
            if (_onFireFullyExtinguished == null)
                return;

            _onFireFullyExtinguished.OnRaised += HandleFireExtinguished;
        }

        private void OnDisable()
        {
            TrainingVrTransientWorldPopup.CancelHideRoutine(this, ref _vrHideRoutine);

            if (_onFireFullyExtinguished != null)
                _onFireFullyExtinguished.OnRaised -= HandleFireExtinguished;
        }

        private void HandleFireExtinguished()
        {
            ResolveInlineStrings(out string title, out string message);

            FireSource fire = TrainingVrTransientSoapFireContext.LastFullyExtinguishedFire;
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
                    VrWorldTrainingCardTone.TrainingSoapSuccess,
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
