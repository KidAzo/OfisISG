using Obvious.Soap;
using UnityEngine;
using Woi.Equipment;
using WOI.Modules.SDK;
using Woi.UI.Popups;
using Woi.UI.Popups.Localization;
using WoiUtils.AudioSystem;

namespace Woi.UI.Announcements
{
    /// <summary>
    /// 3D hover: requires a <see cref="Collider"/> on the same GameObject. Shows localized title/message + Woi audio while “hovering”.
    /// <list type="bullet">
    /// <item><b>Fare / imleç:</b> <see cref="HoverPointerMode.UnityMouseOverCollider"/> — Unity’nin OnMouseEnter/Exit’i imleç pozisyonundan ray kullanır.</item>
    /// <item><b>Fare yok, nişangah:</b> <see cref="HoverPointerMode.CameraCenterRay"/> — sahneye <see cref="ExtinguisherHoverRaycaster"/> ekleyin (genelde ana kamera).</item>
    /// <item><b>VR (sağ kontrolcü):</b> aynı <see cref="HoverPointerMode.CameraCenterRay"/> + <see cref="ExtinguisherHoverTransformRaycaster"/> (ışın transformdan) + sahnede bir <see cref="ExtinguisherHoverVrWorldPopupHost"/>; popup UI Toolkit dünya kartı olarak açılır. Konum her tüpte Inspector’daki VR world popup placement (anchor + yerel offset) ile ayarlanır.</item>
    /// </list>
    /// Tüpler arası geçişte önceki ses ve popup kapanır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ExtinguisherHoverController : MonoBehaviour
    {
        [SerializeField] private HoverPointerMode pointerMode = HoverPointerMode.CameraCenterRay;

        [SerializeField] private LocalizedHoverInfoDefinition content;

        [Header("Inline text (when Content asset is not assigned)")]
        [SerializeField]
        private HoverInfoLanguageSlot _inlineTurkish;

        [SerializeField]
        private HoverInfoLanguageSlot _inlineEnglish;

        [SerializeField] private PopupType popupType = PopupType.Training;
        [SerializeField] private PopupAnchor popupAnchor = PopupAnchor.TopRight;

        [Header("VR world popup placement")]
        [Tooltip(
            "If set, the VR hover card anchors to this transform (e.g. empty child at handle/label height). "
            + "If empty, uses the hit collider’s bounds center (or this object’s Collider).")]
        [SerializeField]
        private Transform vrPopupAnchorOverride;

        [Tooltip("Extra offset in this extinguisher’s local space (e.g. Y+ to float the card). Each prefab can tune independently.")]
        [SerializeField]
        private Vector3 vrPopupOffsetLocal = new Vector3(0f, 0.2f, 0f);

        [Tooltip("If on, the popup uses the ray hit surface normal; if off, uses this transform’s up (often steadier on cylindrical meshes).")]
        [SerializeField]
        private bool vrPopupUseHitSurfaceNormal;

        [Header("Optional gate")]
        [Tooltip("When assigned, hover popup + VO stay off until this Soap event is raised once (e.g. LevelController level-start voice finished). Leave empty to allow hover immediately.")]
        [SerializeField]
        private ScriptableEventNoParam hoverUnlockedAfterLevelNarration;

        private AudioVoice _voice;
        private SoundDefinition _activeHoverSound;

        private static ExtinguisherHoverController _activeHover;

        /// <summary>Set once when <see cref="hoverUnlockedAfterLevelNarration"/> fires; late-spawned replacements skip the wait.</summary>
        private static bool s_levelHoverNarrationReleased;

        /// <summary>Raycasters reset their arm state when the narration gate opens so crosshair-on-target does not auto-play VO.</summary>
        public static event System.Action LevelHoverGateOpened;

        /// <summary>When false, <see cref="TryBeginHover"/> does not show popup or play hover audio.</summary>
        private bool _canHover = true;

        /// <summary>After the narration gate opens, block hover until the ray stops pointing at this collider once.</summary>
        private bool _awaitingPointerLeaveAfterGate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLevelHoverNarrationGate()
        {
            s_levelHoverNarrationReleased = false;
            LevelHoverGateOpened = null;
        }

        /// <summary>True after <see cref="hoverUnlockedAfterLevelNarration"/> has fired at least once this session.</summary>
        public static bool IsLevelHoverGateReleased() => s_levelHoverNarrationReleased;

        /// <summary>Used by <see cref="ExtinguisherHoverRaycaster"/> to skip non–ray-driven tubes.</summary>
        public HoverPointerMode PointerMode => pointerMode;

        private void OnEnable()
        {
            LevelHoverGateOpened += OnInstanceLevelHoverGateOpened;

            if (hoverUnlockedAfterLevelNarration != null)
            {
                if (s_levelHoverNarrationReleased)
                {
                    _canHover = true;
                    _awaitingPointerLeaveAfterGate = true;
                }
                else
                {
                    _canHover = false;
                    hoverUnlockedAfterLevelNarration.OnRaised += OnHoverUnlockedAfterLevelNarrationRaised;
                }
            }
            else
            {
                _canHover = true;
            }
        }

        private void OnHoverUnlockedAfterLevelNarrationRaised()
        {
            s_levelHoverNarrationReleased = true;
            _canHover = true;
            LevelHoverGateOpened?.Invoke();

            if (hoverUnlockedAfterLevelNarration != null)
                hoverUnlockedAfterLevelNarration.OnRaised -= OnHoverUnlockedAfterLevelNarrationRaised;
        }

        private void OnInstanceLevelHoverGateOpened()
        {
            if (hoverUnlockedAfterLevelNarration == null)
                return;

            _awaitingPointerLeaveAfterGate = true;

            if (_activeHover == this)
                NotifyRayHoverEnd();
        }

        private void OnDisable()
        {
            LevelHoverGateOpened -= OnInstanceLevelHoverGateOpened;

            if (hoverUnlockedAfterLevelNarration != null)
                hoverUnlockedAfterLevelNarration.OnRaised -= OnHoverUnlockedAfterLevelNarrationRaised;

            if (_activeHover != this)
                return;

            NotifyRayHoverEnd();
        }

        private void LateUpdate()
        {
            if (_activeHover != this)
                return;
            if (!IsPlayerHoldingExtinguisherForTubeHover())
                return;
            NotifyRayHoverEnd();
        }

        /// <summary>
        /// Invoked via <c>SendMessage("ResetHover")</c> from slot/home replacement spawn. Clears hover audio/popup and re-syncs the narration gate for this instance.
        /// </summary>
        private void ResetHover()
        {
            NotifyRayHoverEnd();

            if (hoverUnlockedAfterLevelNarration != null)
            {
                _canHover = s_levelHoverNarrationReleased;
                _awaitingPointerLeaveAfterGate = s_levelHoverNarrationReleased;
            }
            else
                _canHover = true;
        }

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        private void OnMouseEnter()
        {
            if (pointerMode != HoverPointerMode.UnityMouseOverCollider)
                return;

            TryBeginHoverFromMouse();
        }

        private void OnMouseExit()
        {
            if (pointerMode != HoverPointerMode.UnityMouseOverCollider)
                return;

            TryEndHover();
        }

        bool TryBeginHoverFromMouse() => TryBeginHoverInternal(null, null);

        bool TryBeginHoverFromRay(in RaycastHit hit)
        {
            if (FirePlatformRuntime.IsVR)
                return TryBeginHoverInternal(ComputeVrPopupAnchorWorld(in hit), ComputeVrPopupNormalWorld(in hit));
            return TryBeginHoverInternal(hit.point, hit.normal);
        }

        /// <summary>Called by <see cref="ExtinguisherHoverRaycaster"/> / <see cref="ExtinguisherHoverTransformRaycaster"/> when the ray starts hitting this collider (or a child collider).</summary>
        /// <returns>False if content is missing — raycaster should not treat this object as hovered.</returns>
        public bool NotifyRayHoverBegin(in RaycastHit hit) => TryBeginHoverFromRay(hit);

        /// <summary>Called from VR world popup close button — ends hover the same way as ray exit.</summary>
        public static void EndActiveHoverFromVrWorldUi()
        {
            if (_activeHover != null)
                _activeHover.NotifyRayHoverEnd();
        }

        /// <summary>Called when the ray stops pointing at this collider without an active hover (e.g. gate just opened while crosshair was already on target).</summary>
        public void NotifyRayNotPointingAt()
        {
            _awaitingPointerLeaveAfterGate = false;
        }

        /// <summary>Called by <see cref="ExtinguisherHoverRaycaster"/> when the ray no longer hits this collider. Always stops this instance’s audio; clears popup when we own active hover.</summary>
        public void NotifyRayHoverEnd()
        {
            EndHoverAudio();

            if (_activeHover == this)
            {
                _activeHover = null;

                if (FirePlatformRuntime.IsVR && ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrHost) && vrHost != null)
                    vrHost.Hide();

                if (ServiceLocator.TryGet<IPopupService>(out var popups) && popups != null && popups.IsVisible)
                    popups.Hide();
            }
        }

        /// <summary>When the ray hits nothing eligible this frame — clears stale ownership and closes visible popup (fixes sticky UI).</summary>
        public static void ApplyRayMissCleanup(bool hideVisiblePopup)
        {
            if (_activeHover == null)
                return;

            _activeHover.EndHoverAudio();
            _activeHover = null;

            if (!hideVisiblePopup)
                return;

            if (FirePlatformRuntime.IsVR && ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrHost) && vrHost != null)
                vrHost.Hide();

            if (ServiceLocator.TryGet<IPopupService>(out var popups) && popups != null && popups.IsVisible)
                popups.Hide();
        }

        private bool TryBeginHoverInternal(Vector3? vrHitPoint, Vector3? vrHitNormal)
        {
            if (!_canHover)
                return false;

            if (_awaitingPointerLeaveAfterGate)
                return false;

            if (!TryResolveContentSlot(out HoverInfoLanguageSlot slot))
            {
                Debug.LogWarning("[ExtinguisherHoverController] No hover content (asset or inline TR/EN).", this);
                return false;
            }

            if (IsPlayerHoldingExtinguisherForTubeHover())
                return false;

            if (_activeHover != null && _activeHover != this)
                _activeHover.EndHoverFromPeerSwitch();

            _activeHover = this;

            if (FirePlatformRuntime.IsVR && ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrWorld) && vrWorld != null)
            {
                Vector3 anchor;
                Vector3 normal;
                if (vrHitPoint.HasValue)
                {
                    anchor = vrHitPoint.Value;
                    normal = vrHitNormal ?? Vector3.up;
                }
                else
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        anchor = cam.transform.position + cam.transform.forward * 1.1f;
                        normal = -cam.transform.forward;
                    }
                    else
                    {
                        anchor = transform.position + Vector3.up * 0.5f;
                        normal = Vector3.up;
                    }
                }

                vrWorld.ShowAt(anchor, normal, slot.title ?? string.Empty, slot.message ?? string.Empty);
            }
            else if (ServiceLocator.TryGet<IPopupService>(out var popups) && popups != null)
            {
                popups.ShowTextUntilHidden(
                    slot.title ?? string.Empty,
                    slot.message ?? string.Empty,
                    popupType,
                    popupAnchor);
            }

            StartHoverAudio(slot.sound);
            return true;
        }

        /// <summary>
        /// VR: held extinguisher; PC: equipped extinguisher — rack/world hover popups stay off while carrying gear.
        /// </summary>
        internal static bool IsPlayerHoldingExtinguisherForTubeHover()
        {
            if (VRHandExtinguisherGrabber.GlobalHeldExtinguisherCount > 0)
                return true;

            PlayerExtinguisherEquipment equip =
                UnityEngine.Object.FindFirstObjectByType<PlayerExtinguisherEquipment>(FindObjectsInactive.Exclude);
            return equip != null && equip.CurrentItem != null;
        }

        Vector3 ComputeVrPopupAnchorWorld(in RaycastHit hit)
        {
            Vector3 worldBase;
            if (vrPopupAnchorOverride != null)
                worldBase = vrPopupAnchorOverride.position;
            else
            {
                Collider col = hit.collider != null ? hit.collider : GetComponent<Collider>();
                worldBase = col != null ? col.bounds.center : transform.position;
            }

            return worldBase + transform.TransformVector(vrPopupOffsetLocal);
        }

        Vector3 ComputeVrPopupNormalWorld(in RaycastHit hit)
        {
            if (vrPopupUseHitSurfaceNormal && hit.normal.sqrMagnitude > 1e-8f)
                return hit.normal.normalized;

            Vector3 up = transform.up;
            return up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
        }

        /// <summary>Another tube took focus — stop our audio and hide popup before the new tube shows.</summary>
        private void EndHoverFromPeerSwitch()
        {
            EndHoverAudio();

            if (FirePlatformRuntime.IsVR && ExtinguisherHoverVrWorldPopupHost.TryGetInstance(out var vrHost) && vrHost != null)
                vrHost.Hide();

            if (ServiceLocator.TryGet<IPopupService>(out var popups) && popups != null && popups.IsVisible)
                popups.Hide();
        }

        /// <summary>Unity mouse path — only dismiss if this instance owns the active hover.</summary>
        private void TryEndHover()
        {
            if (_activeHover != this)
                return;

            NotifyRayHoverEnd();
        }

        private void StartHoverAudio(SoundDefinition sound)
        {
            EndHoverAudio();

            if (sound == null)
                return;

            if (!AudioSystem.TryGetFromServiceLocator(out var sys) || sys == null)
                sys = FindFirstObjectByType<AudioSystem>();

            if (sys == null)
            {
                Debug.LogWarning("[ExtinguisherHoverController] No AudioSystem — hover sound skipped.", this);
                return;
            }

            _activeHoverSound = sound;

            var ctx = PlayContext.DebugNoCooldown();
            _voice = sys.Play(sound, ctx);

            // Queue All / delayed Play returns null — audio still runs; stop via SoundDefinition on hover exit.
        }

        /// <summary>VR kart konumu (ör. Class C vana üstü).</summary>
        public void ConfigureVrWorldPopupPlacement(
            Transform anchorOverride,
            Vector3 offsetLocal,
            bool useHitSurfaceNormal)
        {
            vrPopupAnchorOverride = anchorOverride;
            vrPopupOffsetLocal = offsetLocal;
            vrPopupUseHitSurfaceNormal = useHitSurfaceNormal;
        }

        /// <summary>Inspector ile TR/EN metin atamak için.</summary>
        public void SetInlineHoverContent(
            string titleTr,
            string messageTr,
            string titleEn,
            string messageEn,
            SoundDefinition soundTr = null,
            SoundDefinition soundEn = null)
        {
            _inlineTurkish = new HoverInfoLanguageSlot
            {
                title = titleTr,
                message = messageTr,
                sound = soundTr,
            };
            _inlineEnglish = new HoverInfoLanguageSlot
            {
                title = titleEn,
                message = messageEn,
                sound = soundEn,
            };
        }

        bool TryResolveContentSlot(out HoverInfoLanguageSlot slot)
        {
            if (content != null)
            {
                slot = content.ResolveForCurrentLanguage();
                return HasContent(slot);
            }

            slot = PreferTurkishInline() ? _inlineTurkish : _inlineEnglish;
            if (!HasContent(slot))
                slot = PreferTurkishInline() ? _inlineEnglish : _inlineTurkish;

            return HasContent(slot);
        }

        static bool HasContent(HoverInfoLanguageSlot s) =>
            !string.IsNullOrWhiteSpace(s.title)
            || !string.IsNullOrWhiteSpace(s.message)
            || s.sound != null;

        static bool PreferTurkishInline()
        {
            string code = LocalizedLanguageAssetResolver.GetCurrentLanguageCode();

            if (string.IsNullOrWhiteSpace(code))
                return true;

            code = code.Trim().ToLowerInvariant();
            return code == LocalizationService.Turkish || code.StartsWith("tr", System.StringComparison.Ordinal);
        }

        private void EndHoverAudio()
        {
            if (_voice != null)
            {
                _voice.Stop();
                _voice = null;
            }

            if (_activeHoverSound != null)
            {
                if (AudioSystem.TryGetFromServiceLocator(out var sys) && sys != null)
                {
                    sys.StopAllInstances(_activeHoverSound);
                    sys.ClearQueue(_activeHoverSound);
                }
                else
                {
                    AudioSystem fallback = FindFirstObjectByType<AudioSystem>();
                    if (fallback != null)
                    {
                        fallback.StopAllInstances(_activeHoverSound);
                        fallback.ClearQueue(_activeHoverSound);
                    }
                }

                _activeHoverSound = null;
            }
        }
    }
}
