using System;
using System.Collections.Generic;
using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using WOI.Modules.SDK;
using Woi.Equipment;
using Woi.Game.Training.FireSelection;
using Woi.Player;
using Woi.UI.Announcements;
using Woi.UI.Popups;
using WoiUtils.AudioSystem;

namespace Woi.Training
{
    /// <summary>
    /// Player, <see cref="_entries"/> içinde tanımlı (hover bilgisi olan) ve <b>sönmüş olmayan</b> yangınlar için menzil anonsu çalar;
    /// <see cref="TrainingFireSelectionQueries.IsIncludedInTrainingSession"/> ile oturumda seçili olmayan (<see cref="TrainingFireSelectionState"/> kapalı) yangınlara girilmez.
    /// Oyuncunun tuttuğu söndürücülerden <b>biri</b> (<see cref="_pcExtinguisherEquipment"/> / <see cref="_xrExtinguisherEquipment"/> current
    /// veya sabit PC/XR kontrolcü) söndürmeye başladığı anda proximity anons sesi ve popup kesilir (XR’da yalnızca XR alanı değil, atanmış tüm kaynaklar izlenir).
    /// <para>
    /// <b>XR:</b> mesafe sırası: <see cref="_xrPlayerTransform"/> (Inspector, XR Origin) → <see cref="IXRPlayerService.PlayerTransform"/> (kayıtlıysa) → <see cref="_playerTransform"/>.
    /// Ekipman: <see cref="_xrExtinguisherEquipment"/> / <see cref="_xrExtinguisherController"/>; boşsa PC alanları yedek.
    /// Mod: <see cref="IFirePortingPlatformSource"/> varsa <see cref="AppMode.XR"/>; yoksa <see cref="FirePlatformRuntime.IsVR"/>.
    /// Metin: <see cref="ExtinguisherHoverVrWorldPopupHost.ShowAt"/> ile yangın üzerinde/world kartı.
    /// </para>
    /// Liste yangınları tamamen söndüyse veya uygun liste girişi kalmadıysa proximity asla başlamaz.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Proximity Announcement Driver")]
    public sealed class FireProximityAnnouncementDriver : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("PC: oyuncu kökü veya kamera (mesafe bu transformdan).")]
        [SerializeField]
        private Transform _playerTransform;

        [Tooltip("XR: XR Origin / rig kökü (mesafe). Boşsa IXRPlayerService, o da yoksa PC alanındaki _playerTransform yedek olarak kullanılır.")]
        [SerializeField]
        private Transform _xrPlayerTransform;

        [Header("Detection")]
        [Tooltip("World-space distance from player to FireSource transform at which proximity content runs.")]
        [SerializeField]
        [Min(0.1f)]
        private float _proximityRadius = 8f;

        [Header("Extinguisher (source) — PC")]
        [Tooltip("PC: PlayerExtinguisherEquipment — equip/swap/drop ile CurrentItem.Controller güncellenir.")]
        [SerializeField]
        [FormerlySerializedAs("_extinguisherEquipment")]
        private PlayerExtinguisherEquipment _pcExtinguisherEquipment;

        [Tooltip("PC: ekipman yoksa sabit bir ExtinguisherController. Equipment doluysa kullanılmaz.")]
        [SerializeField]
        [FormerlySerializedAs("_extinguisherController")]
        private ExtinguisherController _pcExtinguisherController;

        [Header("Extinguisher (source) — XR")]
        [Tooltip("XR: VR oyuncudaki PlayerExtinguisherEquipment. Boşsa PC alanındaki ekipman yedek.")]
        [SerializeField]
        private PlayerExtinguisherEquipment _xrExtinguisherEquipment;

        [Tooltip("XR: ekipman yoksa sabit kontrolcü. Boşsa PC sabit kontrolcü yedek.")]
        [SerializeField]
        private ExtinguisherController _xrExtinguisherController;

        /// <summary>
        /// <see cref="PlayerExtinguisherEquipment.OnExtinguisherChanged"/> — XR sahnesinde PC + XR ekipmanı ayrı
        /// GameObject’lerde olabildiği için her ikisine de abone olunur; söndürme hangisinden gelirse gelsin proximity kesilir.
        /// </summary>
        private readonly List<PlayerExtinguisherEquipment> _boundExtinguisherEquipments = new(2);

        /// <summary>Spray kesintisi için izlenen tüm kontrolcüler (ekipman current + sabit PC/XR alanları, tekrarsız).</summary>
        private readonly List<ExtinguisherController> _sprayWatchControllers = new(4);

        [Header("Optional narration gate")]
        [Tooltip("When assigned (e.g. LevelNarrationFinishedForHover.asset), proximity popup/VO stay off until this Soap event is raised once. Leave empty to allow proximity immediately.")]
        [SerializeField]
        private ScriptableEventNoParam _levelNarrationFinishedForHover;

        [Header("Presentation (same as Extinguisher Hover)")]
        [SerializeField]
        private PopupType _popupType = PopupType.Training;

        [SerializeField]
        private PopupAnchor _popupAnchor = PopupAnchor.TopRight;

        [Header("VR — yangın üstü world popup")]
        [Tooltip("Collider merkezi kullanılıyorsa aleve hafif yukarı kaydırma (m).")]
        [SerializeField, Min(0f)]
        private float _vrFirePopupLiftFromColliderCenter = 0.2f;

        [Tooltip("Collider yoksa veya Use Collider Center kapalıysa kökten yukarı (m).")]
        [SerializeField, Min(0f)]
        private float _vrFirePopupHeightAboveRoot = 1.35f;

        [Tooltip("En büyük hacimli (non-trigger) child collider merkezini anchor olarak kullan.")]
        [SerializeField]
        private bool _vrFirePopupUseColliderCenter = true;

        [Tooltip("Tüm anchor’lara eklenen dünya Y offset (m); kartı aleve göre yukarı iter. ~0.4 önerilir.")]
        [SerializeField, Min(0f)]
        private float _vrFirePopupExtraWorldYOffset = 0.4f;

        [Tooltip(
            "Normal her zaman dünya yukarısı: kart anchor’dan bu kadar daha yukarı iter (m). " +
            "Eski ‘iz düşümü’ yerine yangının tepesinde dik durması için küçük tutun (ör. 0.05–0.15).")]
        [SerializeField, Min(0f)]
        private float _vrFirePopupSeparationAlongView = 0.08f;

        [Tooltip(
            "Ek olarak kameraya doğru itiş (m). Yangın kartının yüzünüze yapışmasını istemiyorsanız 0 bırakın.")]
        [SerializeField, Min(0f)]
        private float _vrFirePopupTowardViewerExtra = 0f;

        [Tooltip(
            "Collider/kök + Y offset’ten sonra dünya uzayında ek X/Y/Z (m). Proximity kartını sağa/sola/öne-arkaya kaydırmak için.")]
        [SerializeField]
        private Vector3 _vrFirePopupAdditionalWorldOffset;

        [Tooltip(
            "Açıksa kart yangın collider/kökü yerine oyuncu rig’inin yerel uzayında konur. " +
            "Beside Player Local Offset: X = rig sağı, Y = yukarı, Z = rig ileri (metre).")]
        [SerializeField]
        private bool _vrFirePopupPlaceBesidePlayerRig;

        [Tooltip("Place Beside Player Rig açıkken rig köküne göre yerel offset (m). Örn. X=1 → sağda 1 m.")]
        [SerializeField]
        private Vector3 _vrFirePopupBesidePlayerLocalOffsetMeters = new Vector3(0.8f, 1.45f, 0.3f);

        [Header("Fire → localized hover info")]
        [SerializeField]
        private List<FireAnnouncementEntry> _entries = new();

        private FireSource _currentProximityFire;
        private AudioVoice _voice;
        private SoundDefinition _activeSound;

        /// <summary>AudioSystem used for the last <see cref="AudioSystem.Play"/> of proximity VO (normal stop path).</summary>
        private AudioSystem _audioSystemUsedForProximityVoice;

        private bool _canAnnounce = true;

        /// <summary>After spray starts, proximity stays off until the player is outside every configured fire’s radius once.</summary>
        private bool _blockProximityResumeUntilLeaveRadius;

        private float _popupTimeRemaining;
        private bool _isPopupActiveWithSound;
        private int _voiceGeneration;

        private bool _proximityUsesVrWorldPopup;
        private static bool s_warnedVrProximityNoWorldHost;

        [Serializable]
        public sealed class FireAnnouncementEntry
        {
            [Tooltip("Fire instance in the scene.")]
            public FireSource fireSource;

            [Tooltip("Same asset as Extinguisher Hover — Localized Hover Info (EN/TR title, message, sound).")]
            [FormerlySerializedAs("announcement")]
            public LocalizedHoverInfoDefinition hoverInfo;
        }

        private void OnEnable()
        {
            WireExtinguisherBindings();

            if (_levelNarrationFinishedForHover != null)
            {
                _canAnnounce = false;
                _levelNarrationFinishedForHover.OnRaised += OnLevelNarrationFinishedForAnnouncements;
            }
            else
                _canAnnounce = true;
        }

        private void Start()
        {
            // Porting / ServiceLocator bazen diğer bileşenlerin Start'ından sonra hazır olur.
            WireExtinguisherBindings();
        }

        private void OnDisable()
        {
            UnbindAllExtinguisherEquipmentListeners();
            UnbindAllSprayWatchControllers();

            if (_levelNarrationFinishedForHover != null)
                _levelNarrationFinishedForHover.OnRaised -= OnLevelNarrationFinishedForAnnouncements;

            StopProximityPresentationAndClear();
        }

        private void OnLevelNarrationFinishedForAnnouncements()
        {
            _canAnnounce = true;
        }

        private void HandleEquipmentExtinguisherChanged(ExtinguisherPickupItem item) =>
            RebindSprayWatchControllers();

        private void WireExtinguisherBindings()
        {
            UnbindAllExtinguisherEquipmentListeners();
            UnbindAllSprayWatchControllers();

            bool anyEquipment = false;
            TryAddEquipmentListener(_xrExtinguisherEquipment, ref anyEquipment);
            TryAddEquipmentListener(_pcExtinguisherEquipment, ref anyEquipment);

            if (!anyEquipment
                && _xrExtinguisherController == null
                && _pcExtinguisherController == null)
            {
                Debug.LogWarning(
                    "[FireProximityAnnouncementDriver] PlayerExtinguisherEquipment veya sabit ExtinguisherController atanmadı — spray kesintisi kapalı.",
                    this);
            }

            RebindSprayWatchControllers();
        }

        private void TryAddEquipmentListener(PlayerExtinguisherEquipment eq, ref bool anyEquipment)
        {
            if (eq == null)
                return;

            anyEquipment = true;
            if (_boundExtinguisherEquipments.Contains(eq))
                return;

            eq.OnExtinguisherChanged += HandleEquipmentExtinguisherChanged;
            _boundExtinguisherEquipments.Add(eq);
        }

        private void UnbindAllExtinguisherEquipmentListeners()
        {
            for (int i = 0; i < _boundExtinguisherEquipments.Count; i++)
            {
                PlayerExtinguisherEquipment e = _boundExtinguisherEquipments[i];
                if (e != null)
                    e.OnExtinguisherChanged -= HandleEquipmentExtinguisherChanged;
            }

            _boundExtinguisherEquipments.Clear();
        }

        private void RebindSprayWatchControllers()
        {
            UnbindAllSprayWatchControllers();

            AddDistinctSprayWatchController(_xrExtinguisherController);
            AddDistinctSprayWatchController(_pcExtinguisherController);
            AddDistinctSprayWatchFromEquipment(_xrExtinguisherEquipment);
            AddDistinctSprayWatchFromEquipment(_pcExtinguisherEquipment);

            for (int i = 0; i < _sprayWatchControllers.Count; i++)
            {
                ExtinguisherController c = _sprayWatchControllers[i];
                if (c != null)
                    c.OnSprayStarted += HandleExtinguisherSprayStarted;
            }
        }

        private void AddDistinctSprayWatchFromEquipment(PlayerExtinguisherEquipment eq)
        {
            if (eq?.CurrentItem == null)
                return;

            AddDistinctSprayWatchController(eq.CurrentItem.Controller);
        }

        private void AddDistinctSprayWatchController(ExtinguisherController ctrl)
        {
            if (ctrl == null)
                return;

            if (_sprayWatchControllers.Contains(ctrl))
                return;

            _sprayWatchControllers.Add(ctrl);
        }

        private void UnbindAllSprayWatchControllers()
        {
            for (int i = 0; i < _sprayWatchControllers.Count; i++)
            {
                ExtinguisherController c = _sprayWatchControllers[i];
                if (c != null)
                    c.OnSprayStarted -= HandleExtinguisherSprayStarted;
            }

            _sprayWatchControllers.Clear();
        }

        private bool AnyWatchedSprayControllerDischarging()
        {
            for (int i = 0; i < _sprayWatchControllers.Count; i++)
            {
                ExtinguisherController c = _sprayWatchControllers[i];
                if (c != null && c.IsDischarging)
                    return true;
            }

            return false;
        }

        private void HandleExtinguisherSprayStarted()
        {
            StopProximityPresentationAndClear();
            _blockProximityResumeUntilLeaveRadius = true;
        }

        private void Update()
        {
            if (!TryGetProximityCheckWorldPosition(out Vector3 playerWorldPos))
                return;

            if (!HasAnyLiveFireInConfiguredEntries())
            {
                StopProximityPresentationAndClear();
                _blockProximityResumeUntilLeaveRadius = false;
                return;
            }

            // PC ile aynı: söndürmeye başlayınca ses + popup kapanır. XR’da PC/XR ekipmanı ayrı
            // olabildiği için tüm aday kontrolcüler izlenir; IsDischarging yedek olarak her kare kontrol edilir.
            if (AnyWatchedSprayControllerDischarging())
            {
                StopProximityPresentationAndClear();
                _blockProximityResumeUntilLeaveRadius = true;
                return;
            }

            FireSource closest = FindClosestFireInRange(playerWorldPos);

            if (_currentProximityFire != null && !IsFireEligibleForProximityAnnouncement(_currentProximityFire))
                StopProximityPresentationAndClear();

            if (_blockProximityResumeUntilLeaveRadius)
            {
                if (closest == null)
                    _blockProximityResumeUntilLeaveRadius = false;
                else
                {
                    if (_currentProximityFire != null)
                        StopProximityPresentationAndClear();

                    return;
                }
            }

            if (!_canAnnounce)
            {
                if (_currentProximityFire != null)
                    StopProximityPresentationAndClear();

                return;
            }

            if (_isPopupActiveWithSound)
            {
                if (_voice != null)
                {
                    bool isPlaying = _voice.Generation == _voiceGeneration && _voice.IsPlaying();
                    if (!isPlaying)
                    {
                        EndProximityPopupOnly();
                        _isPopupActiveWithSound = false;
                    }
                }
                else
                {
                    _popupTimeRemaining -= Time.deltaTime;
                    if (_popupTimeRemaining <= 0f)
                    {
                        EndProximityPopupOnly();
                        _isPopupActiveWithSound = false;
                    }
                }
            }

            if (closest == _currentProximityFire)
                return;

            if (_currentProximityFire != null)
                StopProximityPresentationAndClear();

            _currentProximityFire = closest;

            if (_currentProximityFire != null)
                TryStartProximityPresentation(_currentProximityFire);
        }

        private void StopProximityPresentationAndClear()
        {
            bool hasProximityState =
                _currentProximityFire != null
                || _isPopupActiveWithSound
                || _proximityUsesVrWorldPopup
                || _voice != null
                || _activeSound != null;

            if (!hasProximityState)
                return;

            StopProximityAnnouncementVoiceEverywhere();
            EndProximityPopupOnly();
            _currentProximityFire = null;
            _isPopupActiveWithSound = false;
        }

        /// <summary>
        /// Yangın bu sürücünün <see cref="_entries"/> listesinde (hover atanmış) tanımlı, oturumda seçili
        /// (<see cref="TrainingFireSelectionQueries.IsIncludedInTrainingSession"/>), sahnede etkin ve henüz sönmüş değilse anons için uygundur.
        /// </summary>
        private bool IsFireEligibleForProximityAnnouncement(FireSource fire)
        {
            if (fire == null || !fire.isActiveAndEnabled || fire.IsExtinguished)
                return false;

            if (!TrainingFireSelectionQueries.IsIncludedInTrainingSession(fire))
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                FireAnnouncementEntry e = _entries[i];
                if (e?.fireSource != fire || e.hoverInfo == null)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>True if at least one entry passes <see cref="IsFireEligibleForProximityAnnouncement"/>.</summary>
        private bool HasAnyLiveFireInConfiguredEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                FireAnnouncementEntry e = _entries[i];
                if (e?.fireSource == null)
                    continue;

                if (IsFireEligibleForProximityAnnouncement(e.fireSource))
                    return true;
            }

            return false;
        }

        private bool TryGetProximityCheckWorldPosition(out Vector3 worldPosition)
        {
            if (IsProximityVrMode())
            {
                if (_xrPlayerTransform != null)
                {
                    worldPosition = _xrPlayerTransform.position;
                    return true;
                }

                if (ServiceLocator.TryGet<IXRPlayerService>(out IXRPlayerService xr)
                    && xr != null
                    && xr.PlayerTransform != null)
                {
                    worldPosition = xr.PlayerTransform.position;
                    return true;
                }

                if (_playerTransform != null)
                {
                    worldPosition = _playerTransform.position;
                    return true;
                }

                worldPosition = default;
                return false;
            }

            if (_playerTransform != null)
            {
                worldPosition = _playerTransform.position;
                return true;
            }

            worldPosition = default;
            return false;
        }

        /// <summary>
        /// Porting SO (varsa) öncelikli; aksi halde <see cref="FirePlatformRuntime"/> — XR sahnesinde PC transformu yanlışlıkla kullanılmasın diye.
        /// </summary>
        private static bool IsProximityVrMode()
        {
            if (ServiceLocator.TryGet<IFirePortingPlatformSource>(out var porting) && porting != null)
                return porting.CurrentMode == AppMode.XR;
            return FirePlatformRuntime.IsVR;
        }

        private FireSource FindClosestFireInRange(Vector3 playerWorldPos)
        {
            Vector3 p = playerWorldPos;
            float r2 = _proximityRadius * _proximityRadius;
            FireSource best = null;
            float bestD2 = float.MaxValue;

            for (int i = 0; i < _entries.Count; i++)
            {
                FireAnnouncementEntry e = _entries[i];
                if (e?.fireSource == null || !IsFireEligibleForProximityAnnouncement(e.fireSource))
                    continue;

                float d2 = (e.fireSource.transform.position - p).sqrMagnitude;
                bool forcedCriticalVolume = ForcedCriticalProximityRegistry.IsForcedFor(e.fireSource);
                
                // Hysteresis: prevent flickering at the radius boundary or between two equidistant fires
                float allowedR2 = r2;
                float effectiveD2 = d2;
                
                if (e.fireSource == _currentProximityFire)
                {
                    allowedR2 = (_proximityRadius + 1.5f) * (_proximityRadius + 1.5f); // 1.5m buffer to leave
                    effectiveD2 -= 4f; // Give current fire a strong distance advantage
                }

                if (!forcedCriticalVolume && d2 > allowedR2)
                    continue;

                if (effectiveD2 >= bestD2)
                    continue;

                bestD2 = effectiveD2;
                best = e.fireSource;
            }

            return best;
        }

        private bool TryGetEntry(FireSource fire, out LocalizedHoverInfoDefinition hover)
        {
            hover = null;
            if (fire == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                FireAnnouncementEntry e = _entries[i];
                if (e?.fireSource != fire || e.hoverInfo == null)
                    continue;

                hover = e.hoverInfo;
                return true;
            }

            return false;
        }

        private bool TryComputeVrFirePopupPlacement(FireSource fire, out Vector3 anchor, out float? worldDocumentScaleMultiplier)
        {
            var layout = BuildVrFirePopupLayout();
            return TrainingVrFireWorldCardPlacement.TryComputeAnchor(
                fire,
                in layout,
                out anchor,
                out worldDocumentScaleMultiplier);
        }

        TrainingVrFireWorldCardPlacement.Layout BuildVrFirePopupLayout() =>
            new TrainingVrFireWorldCardPlacement.Layout(
                _vrFirePopupUseColliderCenter,
                _vrFirePopupLiftFromColliderCenter,
                _vrFirePopupHeightAboveRoot,
                _vrFirePopupExtraWorldYOffset,
                _vrFirePopupAdditionalWorldOffset,
                _vrFirePopupPlaceBesidePlayerRig,
                _vrFirePopupBesidePlayerLocalOffsetMeters);

        private void TryStartProximityPresentation(FireSource fire)
        {
            if (!IsFireEligibleForProximityAnnouncement(fire))
                return;

            if (!TryGetEntry(fire, out LocalizedHoverInfoDefinition hoverAsset))
            {
                Debug.LogWarning(
                    $"[FireProximityAnnouncementDriver] No Localized Hover Info assigned for fire '{fire.name}'.",
                    this);
                return;
            }

            HoverInfoLanguageSlot slot = hoverAsset.ResolveForCurrentLanguage();

            _proximityUsesVrWorldPopup = false;

            if (IsProximityVrMode())
            {
                if (ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrHost) && vrHost != null)
                {
                    if (TryComputeVrFirePopupPlacement(fire, out Vector3 anchor, out float? scaleMul))
                    {
                        vrHost.ShowAt(
                            anchor,
                            Vector3.up,
                            slot.title ?? string.Empty,
                            slot.message ?? string.Empty,
                            _vrFirePopupSeparationAlongView,
                            _vrFirePopupTowardViewerExtra,
                            worldDocumentScaleMultiplier: scaleMul ?? float.NaN);
                    }
                    else
                    {
                        vrHost.ShowAt(
                            fire.transform.position
                            + Vector3.up * (_vrFirePopupHeightAboveRoot + _vrFirePopupExtraWorldYOffset)
                            + _vrFirePopupAdditionalWorldOffset,
                            Vector3.up,
                            slot.title ?? string.Empty,
                            slot.message ?? string.Empty,
                            _vrFirePopupSeparationAlongView,
                            _vrFirePopupTowardViewerExtra,
                            worldDocumentScaleMultiplier: float.NaN);
                    }

                    _proximityUsesVrWorldPopup = true;
                }
                else if (!s_warnedVrProximityNoWorldHost)
                {
                    s_warnedVrProximityNoWorldHost = true;
                    Debug.LogWarning(
                        "[FireProximityAnnouncementDriver] VR: sahnede ExtinguisherHoverVrWorldPopupHost yok (tüp hover ile aynı world kartı). Proximity metni gösterilmedi; ses yine çalar.",
                        this);
                }
            }
            else if (ServiceLocator.TryGet<IPopupService>(out var popups) && popups != null)
            {
                popups.ShowTextUntilHidden(
                    slot.title ?? string.Empty,
                    slot.message ?? string.Empty,
                    _popupType,
                    _popupAnchor);
            }

            StartProximityAudio(slot.sound);

            _popupTimeRemaining = 2f;
            _isPopupActiveWithSound = true;
            if (_voice != null)
                _voiceGeneration = _voice.Generation;
        }

        private void StartProximityAudio(SoundDefinition sound)
        {
            EndProximityVoiceOnly();

            if (sound == null)
                return;

            if (!AudioSystem.TryGetFromServiceLocator(out var sys) || sys == null)
                sys = FindFirstObjectByType<AudioSystem>();

            if (sys == null)
            {
                Debug.LogWarning("[FireProximityAnnouncementDriver] No AudioSystem — proximity voice skipped.", this);
                return;
            }

            _audioSystemUsedForProximityVoice = sys;
            _activeSound = sound;
            var ctx = PlayContext.DebugNoCooldown();
            _voice = sys.Play(sound, ctx);
        }

        /// <summary>Proximity VO: önce tutulan voice, sonra bu ses için sahnedeki tüm <see cref="AudioSystem"/> örneklerinde durdur + kuyruk temizle.</summary>
        private void StopProximityAnnouncementVoiceEverywhere()
        {
            if (_voice != null)
            {
                _voice.Stop();
                _voice = null;
            }

            if (_activeSound == null)
            {
                _audioSystemUsedForProximityVoice = null;
                return;
            }

            SoundDefinition sd = _activeSound;
            AudioSystem[] systems = FindObjectsByType<AudioSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                AudioSystem audioSys = systems[i];
                if (audioSys == null)
                    continue;

                audioSys.StopAllInstances(sd);
                audioSys.ClearQueue(sd);
            }

            _activeSound = null;
            _audioSystemUsedForProximityVoice = null;
        }

        private void EndProximityVoiceOnly()
        {
            if (_voice != null)
            {
                _voice.Stop();
                _voice = null;
            }

            if (_activeSound != null)
            {
                AudioSystem sys = _audioSystemUsedForProximityVoice;
                if (sys == null)
                {
                    if (AudioSystem.TryGetFromServiceLocator(out var s) && s != null)
                        sys = s;
                    else
                        sys = FindFirstObjectByType<AudioSystem>();
                }

                if (sys != null)
                {
                    sys.StopAllInstances(_activeSound);
                    sys.ClearQueue(_activeSound);
                }

                _audioSystemUsedForProximityVoice = null;
                _activeSound = null;
            }
        }

        private void EndProximityPopupOnly()
        {
            if (!_isPopupActiveWithSound && !_proximityUsesVrWorldPopup)
                return;

            if (_proximityUsesVrWorldPopup && ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrHost) && vrHost != null)
                vrHost.Hide();

            if (!_proximityUsesVrWorldPopup
                && ServiceLocator.TryGet<IPopupService>(out var popups)
                && popups != null
                && popups.IsVisible)
                popups.Hide();

            _proximityUsesVrWorldPopup = false;
        }

    }
}
