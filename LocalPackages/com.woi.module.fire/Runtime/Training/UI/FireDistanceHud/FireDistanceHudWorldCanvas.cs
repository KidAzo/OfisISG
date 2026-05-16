using System;
using System.Collections.Generic;
using FireExtinguisher.Core;
using Obvious.Soap;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Woi.Game.Training.FireSelection;
using Woi.Training;

namespace Woi.Game.Training.UI.FireDistance
{
    /// <summary>Band used by <see cref="FireDistanceHudWorldCanvas"/> from player–fire distance.</summary>
    public enum FireHudDistanceState
    {
        Away,
        Ready,
        Critical
    }

    /// <summary>Inspector-driven visuals for one distance band (message copy + colors).</summary>
    [Serializable]
    public sealed class FireHudStateVisualConfig
    {
        [SerializeField] public FireHudDistanceState state;

        [Tooltip("Shown in the message label (e.g. box around ‘SCANNING ZONE’).")]
        [SerializeField] public string messageText = string.Empty;

        [SerializeField] public Color mainColorA = Color.white;
        [SerializeField] public Color mainColorB = Color.white;
        [SerializeField, Min(0.01f)] public float mainFadeSpeed = 2f;

        [SerializeField] public Color flameColorA = Color.white;
        [SerializeField] public Color flameColorB = Color.white;
        [SerializeField, Min(0.01f)] public float flameFadeSpeed = 2f;

        [Header("Inside background (semi-circle fill)")]
        [Tooltip("Per-state fill tint; lerps A ↔ B when Enable Blink is on.")]
        [SerializeField] public Color insideBackgroundColorA = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] public Color insideBackgroundColorB = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField, Min(0.01f)] public float insideFadeSpeed = 2f;

        [SerializeField] public bool enableBlink = true;
    }

    /// <summary>
    /// World-space Canvas HUD: <see cref="distanceText"/> (<c>Dist.</c> prefix) + <see cref="messageText"/>,
    /// <see cref="flameIconImage"/>, <see cref="insideBackgroundImage"/> (semi-circle / fill), and one outline image
    /// per state. Transform (position, rotation, scale) is authored only in the Editor — this component does not
    /// modify <c>Transform</c>.
    /// Oyuncu konumu: önce porting (XR = <see cref="TrainingPlayerAnchorResolver"/> rig / XROrigin; PC = IPlayerService), yoksa Inspector’daki <c>playerTarget</c> yedeği.
    /// Each fire that needs its own HUD must use its own UI references (duplicate the widgets under that fire’s
    /// <see cref="hudRoot"/>). Multiple <see cref="FireDistanceHudWorldCanvas"/> instances must not point at the same
    /// <see cref="Image"/> / <see cref="TextMeshProUGUI"/> instances — they would overwrite each other every frame
    /// (looks like “only one fire’s state works”); that is not a material instancing issue.
    /// <para>
    /// <b>Danger hooks:</b> <see cref="onCriticalBandEntered"/> / <see cref="onCriticalBandExited"/> fire when the
    /// distance band crosses to/from <see cref="FireHudDistanceState.Critical"/> (kırmızı HUD bandı).
    /// <see cref="onMinimumApproachPushApplied"/> fires once when <see cref="enforceMinimumApproachFromFire"/> pushes
    /// the player outward after <see cref="pushDelayAfterTooCloseSeconds"/> — BurnScreen / tam ekran efektleri buraya veya SOAP’a bağlanabilir.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("WOI/Training/UI/Fire Distance HUD (World Canvas)")]
    public sealed class FireDistanceHudWorldCanvas : MonoBehaviour
    {
        [Header("Validation")]
        [Tooltip("When true, skips the runtime check that normally errors if two FireDistanceHudWorldCanvas components reference the same Image/Text. " +
                 "Prefer duplicating the HUD UI under each fire’s hierarchy with unique references. " +
                 "If you enable this while references are still shared, both components will keep overwriting the same widgets.")]
        [SerializeField] private bool _ignoreSharedHudGraphicValidation;

        [Header("References")]
        [SerializeField] private FireSource fireSource;

        [Tooltip(
            "İsteğe bağlı yedek: porting ile çözülen oyuncu kökü (XR = rig veya XROrigin; PC = IPlayerService) yoksa kullanılır. XR’da genelde boş bırakılabilir.")]
        [SerializeField]
        private Transform playerTarget;

        [Tooltip("Used for show/hide only — position, rotation, and scale are authored in the scene / prefab. For several fires, each HUD should use graphics under this root (or unique references), not the same Image/Text as another FireDistanceHudWorldCanvas.")]
        [SerializeField] private Transform hudRoot;
        [SerializeField] private Canvas worldCanvas;

        [Header("Images — single flame, no secondary flame")]
        [Tooltip("One flame icon only (outline/sprite). Must not be shared with another FireDistanceHudWorldCanvas on a different fire.")]
        [SerializeField] private Image flameIconImage;

        [Tooltip("Semi-circle / fan fill; tint comes from each state's Inside Background Color A/B.")]
        [SerializeField] private Image insideBackgroundImage;

        [Header("Message outlines — one per state")]
        [Tooltip("Shown only in Away; typically the border around the message area.")]
        [SerializeField] private Image outlineAwayImage;
        [Tooltip("Shown only in Ready.")]
        [SerializeField] private Image outlineReadyImage;
        [Tooltip("Shown only in Critical.")]
        [SerializeField] private Image outlineCriticalImage;

        [Header("Texts — distance + message only")]
        [SerializeField] private TextMeshProUGUI distanceText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Distance (m)")]
        [SerializeField, Min(0.01f)] private float readyDistance = 4f;
        [SerializeField, Min(0.01f)] private float criticalDistance = 1.2f;

        [Header("Minimum approach (uses Critical Distance)")]
        [Tooltip(
            "When enabled, if the player stays closer than <b>Critical Distance</b> (m) for <see cref=\"pushDelayAfterTooCloseSeconds\"/>, they are pushed once outward " +
            "(same threshold as HUD Critical). Leaving that radius before the delay resets the timer — no push.")]
        [SerializeField] private bool enforceMinimumApproachFromFire;

        [Tooltip("Extra meters beyond Critical Distance after a push (avoids immediate re-entry).")]
        [SerializeField, Min(0f)] private float pushPastCriticalExtraMeters = 0.08f;

        [Tooltip("Oyuncu critical mesafeden içeride kaldığı süre (sn, unscaled) bu kadar olunca bir kez dışarı itilir. Daha erken çıkarsa süre sıfırlanır, itiş olmaz.")]
        [SerializeField, Min(0f)] private float pushDelayAfterTooCloseSeconds = 2.5f;

        [Header("Visibility")]
        [Tooltip("Oyuncu–yangın mesafesi bu yarıçapın (m) içindeyken HUD açılır — FireProximityAnnouncementDriver._proximityRadius ile aynı mantık. 0 = bu iç sınır kapalı (sadece Max View Distance veya limitsiz).")]
        [SerializeField, Min(0f)]
        private float proximityRadius = 8f;

        [Tooltip("İsteğe bağlı dış üst sınır (m). >0 ise oyuncu bu mesafeden uzaktayken HUD gizlenir. 0 = dış sınır yok.")]
        [SerializeField, Min(0f)]
        private float maxViewDistance = 30f;
        [SerializeField] private bool hideWhenExtinguished = true;
        [SerializeField] private bool hideWhenFireNotSelected = true;

        [Header("Forced critical zone")]
        [Tooltip(
            "<see cref=\"FireCriticalProximityVolume\"/> ile eşleşen yangında: oyuncu gerçek mesafede proximity dışında olsa bile HUD açılır ve bant <b>Critical</b> (kırmızı) gibi işlenir.")]
        [SerializeField] private bool showHudWhenInForcedCriticalVolume = true;

        [Header("Fire danger hooks (Critical HUD + minimum-approach push)")]
        [Tooltip("Oyuncu mesafe bandı Critical'e geçtiğinde (kırmızı / critical outline).")]
        [SerializeField]
        private UnityEvent onCriticalBandEntered;

        [Tooltip("Critical bandından çıkınca (Ready veya Away).")]
        [SerializeField]
        private UnityEvent onCriticalBandExited;

        [SerializeField]
        private ScriptableEventNoParam onCriticalBandEnteredSoap;

        [SerializeField]
        private ScriptableEventNoParam onCriticalBandExitedSoap;

        [Tooltip("Minimum yaklaşım açıkken gecikmeden sonra oyuncu dışarı itildiğinde (burst başına bir kez).")]
        [SerializeField]
        private UnityEvent onMinimumApproachPushApplied;

        [SerializeField]
        private ScriptableEventNoParam onMinimumApproachPushAppliedSoap;

        [Header("State visuals")]
        [SerializeField] private List<FireHudStateVisualConfig> stateConfigs = new List<FireHudStateVisualConfig>();

        private FireHudDistanceState _currentState = FireHudDistanceState.Away;
        private FireHudStateVisualConfig _activeConfig;
        private bool _wasHudShownLastFrame;
        private bool _warnedMissingFireSource;
        private bool _warnedMissingPlayer;
        private bool _warnedMissingHudRoot;
        private bool _warnedMissingConfigForState;

        private float _tooCloseTimerUnscaled;
        private bool _pushedThisTooCloseBurst;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool s_warnedSharedHudGraphics;
#endif

        private void Awake()
        {
            EnsureAllStatesHaveConfig();
            ApplyCopyForState(_currentState);
            RefreshOutlineActivation(_currentState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_ignoreSharedHudGraphicValidation)
                WarnIfGraphicsAreSharedAcrossHuds();
#endif
        }

        private void OnValidate()
        {
            if (criticalDistance >= readyDistance)
            {
                Debug.LogWarning(
                    $"[{nameof(FireDistanceHudWorldCanvas)}] criticalDistance should be less than readyDistance. Clamping.",
                    this);
                criticalDistance = Mathf.Min(criticalDistance, readyDistance - 0.01f);
                if (criticalDistance < 0.01f)
                    criticalDistance = 0.01f;
            }

            EnsureAllStatesHaveConfig();
        }

        private void Reset()
        {
            EnsureAllStatesHaveConfig();
        }

        private void LateUpdate()
        {
            if (!EvaluateFireAndHudReferences())
                return;

            Transform playerRoot = ResolvePlayerRootForHud();
            if (playerRoot == null)
            {
                if (!_warnedMissingPlayer)
                {
                    _warnedMissingPlayer = true;
                    Debug.LogWarning(
                        $"[{nameof(FireDistanceHudWorldCanvas)}] Oyuncu kökü bulunamadı: porting (XR rig / XROrigin veya PC IPlayerService) ve " +
                        $"'{nameof(playerTarget)}' yedeği boş. Mesafe HUD’u çalışmıyor.",
                        this);
                }

                return;
            }

            float distance = Vector3.Distance(fireSource.transform.position, playerRoot.position);

            if (enforceMinimumApproachFromFire)
            {
                if (EvaluateVisibility())
                    distance = TryPushPlayerOutOfCriticalRadius(distance, playerRoot);
                else
                    ResetMinimumApproachPushState();
            }
            else
            {
                ResetMinimumApproachPushState();
            }

            bool forcedCritical = ForcedCriticalProximityRegistry.IsForcedFor(fireSource);
            float stateDistance = GetStateEvaluationDistance(distance, forcedCritical);

            bool show = EvaluateVisibility() && IsPlayerWithinHudRange(distance, forcedCritical);
            SetHudActive(show);
            if (!show)
            {
                _wasHudShownLastFrame = false;
                return;
            }

            if (!_wasHudShownLastFrame)
            {
                _wasHudShownLastFrame = true;
                FireHudDistanceState sync = ResolveState(stateDistance);
                FireHudDistanceState previous = _currentState;
                _currentState = sync;
                RaiseCriticalBandTransitionsIfChanged(previous, _currentState);
                ApplyCopyForState(_currentState);
                RefreshOutlineActivation(_currentState);
            }

            if (distanceText != null)
            {
                string suffix = forcedCritical ? " !" : string.Empty;
                distanceText.text = $"Dist. {distance:F1} m{suffix}";
            }

            FireHudDistanceState newState = ResolveState(stateDistance);
            if (newState != _currentState)
            {
                FireHudDistanceState previous = _currentState;
                _currentState = newState;
                RaiseCriticalBandTransitionsIfChanged(previous, _currentState);
                ApplyCopyForState(_currentState);
                RefreshOutlineActivation(_currentState);
            }

            FireHudStateVisualConfig cfg = ResolveActiveConfig();
            if (cfg == null)
                return;

            Color mainColor;
            Color flameColor;
            Color insideBgColor;

            if (cfg.enableBlink)
            {
                float mainT = Mathf.PingPong(Time.time * cfg.mainFadeSpeed, 1f);
                float flameT = Mathf.PingPong(Time.time * cfg.flameFadeSpeed, 1f);
                float insideT = Mathf.PingPong(Time.time * cfg.insideFadeSpeed, 1f);
                mainColor = Color.Lerp(cfg.mainColorA, cfg.mainColorB, mainT);
                flameColor = Color.Lerp(cfg.flameColorA, cfg.flameColorB, flameT);
                insideBgColor = Color.Lerp(cfg.insideBackgroundColorA, cfg.insideBackgroundColorB, insideT);
            }
            else
            {
                mainColor = cfg.mainColorA;
                flameColor = cfg.flameColorA;
                insideBgColor = cfg.insideBackgroundColorA;
            }

            if (IsInsideBackgroundUnset(cfg))
                insideBgColor = mainColor;

            ApplyMainColor(mainColor, _currentState);
            ApplyInsideBackgroundColor(insideBgColor);
            ApplyFlameColor(flameColor);
        }

        /// <summary>Önce porting (XR rig / XROrigin veya PC oyuncu kökü); yoksa Inspector <see cref="playerTarget"/> yedeği.</summary>
        Transform ResolvePlayerRootForHud()
        {
            if (TrainingPlayerAnchorResolver.TryGetAnchorWorldTransform(out Transform t))
                return t;
            return playerTarget;
        }

        private bool EvaluateFireAndHudReferences()
        {
            if (fireSource == null)
            {
                if (!_warnedMissingFireSource)
                {
                    _warnedMissingFireSource = true;
                    Debug.LogWarning($"[{nameof(FireDistanceHudWorldCanvas)}] FireSource is not assigned.", this);
                }

                return false;
            }

            if (hudRoot == null)
            {
                if (!_warnedMissingHudRoot)
                {
                    _warnedMissingHudRoot = true;
                    Debug.LogWarning($"[{nameof(FireDistanceHudWorldCanvas)}] HUD root transform is not assigned.", this);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// After <see cref="pushDelayAfterTooCloseSeconds"/> of continuous "too close" (inside critical radius), pushes once until the player leaves that radius.
        /// </summary>
        /// <returns>Player–fire distance after any correction.</returns>
        private float TryPushPlayerOutOfCriticalRadius(float distance, Transform playerRoot)
        {
            if (fireSource == null || playerRoot == null)
                return distance;

            float minRadius = Mathf.Max(0.01f, criticalDistance);
            const float eps = 0.0005f;
            bool tooClose = distance < minRadius - eps;

            if (!tooClose)
            {
                ResetMinimumApproachPushState();
                return distance;
            }

            if (_pushedThisTooCloseBurst)
                return distance;

            if (pushDelayAfterTooCloseSeconds > 0f)
            {
                _tooCloseTimerUnscaled += Time.unscaledDeltaTime;
                if (_tooCloseTimerUnscaled < pushDelayAfterTooCloseSeconds)
                    return distance;
            }

            Vector3 firePos = fireSource.transform.position;
            Vector3 playerPos = playerRoot.position;
            Vector3 fromFire = playerPos - firePos;
            float mag = fromFire.magnitude;

            Vector3 dir;
            if (mag < 1e-5f)
            {
                dir = Vector3.Cross(Vector3.up, fireSource.transform.forward);
                if (dir.sqrMagnitude < 1e-6f)
                    dir = Vector3.forward;
                dir.Normalize();
            }
            else
            {
                dir = fromFire / mag;
            }

            float targetDist = minRadius + Mathf.Max(0f, pushPastCriticalExtraMeters);
            Vector3 desiredWorld = firePos + dir * targetDist;
            Vector3 delta = desiredWorld - playerPos;

            ApplyWorldDeltaToPlayer(delta, playerRoot);

            onMinimumApproachPushApplied?.Invoke();
            onMinimumApproachPushAppliedSoap?.Raise();

            _pushedThisTooCloseBurst = true;
            _tooCloseTimerUnscaled = 0f;

            return Vector3.Distance(fireSource.transform.position, playerRoot.position);
        }

        private void ResetMinimumApproachPushState()
        {
            _tooCloseTimerUnscaled = 0f;
            _pushedThisTooCloseBurst = false;
        }

        void RaiseCriticalBandTransitionsIfChanged(FireHudDistanceState previous, FireHudDistanceState next)
        {
            if (previous == next)
                return;

            bool wasCritical = previous == FireHudDistanceState.Critical;
            bool nowCritical = next == FireHudDistanceState.Critical;

            if (wasCritical && !nowCritical)
            {
                onCriticalBandExited?.Invoke();
                onCriticalBandExitedSoap?.Raise();
            }

            if (!wasCritical && nowCritical)
            {
                onCriticalBandEntered?.Invoke();
                onCriticalBandEnteredSoap?.Raise();
            }
        }

        private void ApplyWorldDeltaToPlayer(Vector3 worldDelta, Transform playerRoot)
        {
            if (worldDelta.sqrMagnitude < 1e-10f || playerRoot == null)
                return;

            // XR: IXRPlayerService bazen Camera Offset / göz altı transform döner; itişi tüm rig’e uygula (PC’deki gibi).
            XROrigin origin = playerRoot.GetComponent<XROrigin>()
                ?? playerRoot.GetComponentInParent<XROrigin>()
                ?? playerRoot.GetComponentInChildren<XROrigin>(true);
            if (origin != null)
            {
                Transform rig = origin.transform;
                CharacterController cc = rig.GetComponent<CharacterController>()
                    ?? rig.GetComponentInChildren<CharacterController>(true);
                if (cc != null && cc.enabled)
                {
                    cc.Move(worldDelta);
                    return;
                }

                rig.position += worldDelta;
                return;
            }

            Transform t = playerRoot;

            CharacterController ccPc = t.GetComponent<CharacterController>()
                ?? t.GetComponentInParent<CharacterController>()
                ?? t.GetComponentInChildren<CharacterController>(true);
            if (ccPc != null && ccPc.enabled)
            {
                ccPc.Move(worldDelta);
                return;
            }

            Rigidbody rb = t.GetComponent<Rigidbody>()
                ?? t.GetComponentInParent<Rigidbody>()
                ?? t.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.MovePosition(rb.position + worldDelta);
                return;
            }

            t.position += worldDelta;
        }

        private bool EvaluateVisibility()
        {
            if (hideWhenExtinguished && fireSource.IsExtinguished)
                return false;

            if (hideWhenFireNotSelected && !TrainingFireSelectionQueries.IsIncludedInTrainingSession(fireSource))
                return false;

            return true;
        }

        /// <summary>
        /// <see cref="proximityRadius"/> içinde olmalı (&gt;0 ise); ayrıca <see cref="maxViewDistance"/> varsa onun altında olmalı.
        /// <paramref name="forcedCritical"/> true iken <see cref="showHudWhenInForcedCriticalVolume"/> açıksa proximity kontrolü atlanır (maxViewDistance hâlâ uygulanır).
        /// </summary>
        private bool IsPlayerWithinHudRange(float distanceMeters, bool forcedCritical)
        {
            if (forcedCritical && showHudWhenInForcedCriticalVolume)
            {
                if (maxViewDistance > 0f && distanceMeters > maxViewDistance)
                    return false;

                return true;
            }

            if (proximityRadius > 0f && distanceMeters > proximityRadius)
                return false;

            if (maxViewDistance > 0f && distanceMeters > maxViewDistance)
                return false;

            return true;
        }

        /// <summary>
        /// Zorunlu kritik hacimdeyken bant hesabı için mesafeyi critical altına indirger (HUD kırmızı / critical kopyası).
        /// </summary>
        private float GetStateEvaluationDistance(float workingDistanceMeters, bool forcedCritical)
        {
            if (!forcedCritical)
                return workingDistanceMeters;

            float cap = Mathf.Max(0.01f, criticalDistance) * 0.5f;
            return Mathf.Min(workingDistanceMeters, cap);
        }

        private void SetHudActive(bool visible)
        {
            if (hudRoot != null && hudRoot.gameObject.activeSelf != visible)
                hudRoot.gameObject.SetActive(visible);

            if (worldCanvas != null && worldCanvas.enabled != visible)
                worldCanvas.enabled = visible;
        }

        private static FireHudDistanceState ResolveState(float distance, float ready, float critical)
        {
            if (distance <= critical)
                return FireHudDistanceState.Critical;

            if (distance > ready)
                return FireHudDistanceState.Away;

            return FireHudDistanceState.Ready;
        }

        private FireHudDistanceState ResolveState(float distance)
            => ResolveState(distance, readyDistance, criticalDistance);

        private FireHudStateVisualConfig ResolveActiveConfig()
        {
            if (_activeConfig != null && _activeConfig.state == _currentState)
                return _activeConfig;

            _activeConfig = FindConfigOrFallback(_currentState);
            return _activeConfig;
        }

        private FireHudStateVisualConfig FindConfigOrFallback(FireHudDistanceState state)
        {
            FireHudStateVisualConfig c = FindConfig(state);
            if (c != null)
                return c;

            c = FindConfig(FireHudDistanceState.Away);
            if (c != null)
                return c;

            if (stateConfigs != null)
            {
                for (int i = 0; i < stateConfigs.Count; i++)
                {
                    if (stateConfigs[i] != null)
                        return stateConfigs[i];
                }
            }

            return null;
        }

        private FireHudStateVisualConfig FindConfig(FireHudDistanceState state)
        {
            if (stateConfigs == null)
                return null;

            for (int i = 0; i < stateConfigs.Count; i++)
            {
                FireHudStateVisualConfig c = stateConfigs[i];
                if (c != null && c.state == state)
                    return c;
            }

            return null;
        }

        private void ApplyCopyForState(FireHudDistanceState state)
        {
            FireHudStateVisualConfig cfg = FindConfig(state);
            if (cfg == null)
            {
                if (!_warnedMissingConfigForState)
                {
                    _warnedMissingConfigForState = true;
                    Debug.LogWarning(
                        $"[{nameof(FireDistanceHudWorldCanvas)}] No {nameof(FireHudStateVisualConfig)} for state '{state}'. Add an entry in stateConfigs (defaults are filled in Awake/OnValidate when possible).",
                        this);
                }

                cfg = FindConfigOrFallback(state);
            }

            _activeConfig = cfg;

            if (cfg == null)
                return;

            if (messageText != null)
                messageText.text = cfg.messageText ?? string.Empty;
        }

        /// <summary>Exactly one outline image active for the current band (each state can use its own sprite/asset).</summary>
        private void RefreshOutlineActivation(FireHudDistanceState state)
        {
            SetOutlineVisible(outlineAwayImage, state == FireHudDistanceState.Away);
            SetOutlineVisible(outlineReadyImage, state == FireHudDistanceState.Ready);
            SetOutlineVisible(outlineCriticalImage, state == FireHudDistanceState.Critical);
        }

        private static void SetOutlineVisible(Image outline, bool visible)
        {
            if (outline != null)
                outline.gameObject.SetActive(visible);
        }

        /// <summary>Legacy configs / unset fields: treat as “use main HUD pulse” for the fill.</summary>
        private static bool IsInsideBackgroundUnset(FireHudStateVisualConfig cfg)
        {
            if (cfg == null)
                return true;

            const float eps = 0.001f;
            return cfg.insideBackgroundColorA.maxColorComponent <= eps
                   && cfg.insideBackgroundColorB.maxColorComponent <= eps;
        }

        /// <summary>Main tint: distance + message + active outline only.</summary>
        private void ApplyMainColor(Color color, FireHudDistanceState state)
        {
            SetTmpColor(distanceText, color);
            SetTmpColor(messageText, color);

            if (state == FireHudDistanceState.Away)
                SetImageColor(outlineAwayImage, color);
            else if (state == FireHudDistanceState.Ready)
                SetImageColor(outlineReadyImage, color);
            else
                SetImageColor(outlineCriticalImage, color);
        }

        private void ApplyInsideBackgroundColor(Color color)
        {
            SetImageColor(insideBackgroundImage, color);
        }

        private void ApplyFlameColor(Color color)
        {
            SetImageColor(flameIconImage, color);
        }

        private static void SetImageColor(Image image, Color color)
        {
            // Do not gate on activeInHierarchy: inactive outline images still need the correct tint before they are shown.
            if (image != null)
                image.color = color;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void WarnIfGraphicsAreSharedAcrossHuds()
        {
            if (s_warnedSharedHudGraphics)
                return;

            FireDistanceHudWorldCanvas[] huds = FindObjectsByType<FireDistanceHudWorldCanvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < huds.Length; i++)
            {
                FireDistanceHudWorldCanvas a = huds[i];
                if (a == null)
                    continue;

                for (int j = i + 1; j < huds.Length; j++)
                {
                    FireDistanceHudWorldCanvas b = huds[j];
                    if (b == null)
                        continue;

                    if (!a.SharesAnyDrivenGraphicWith(b))
                        continue;

                    if (a._ignoreSharedHudGraphicValidation || b._ignoreSharedHudGraphicValidation)
                        continue;

                    s_warnedSharedHudGraphics = true;
                    Debug.LogError(
                        $"[{nameof(FireDistanceHudWorldCanvas)}] Two or more HUD components reference the same UI element "
                        + $"(Image/Text/Canvas). Each fire needs its own UI instances under that fire’s hierarchy, or remove "
                        + $"extra {nameof(FireDistanceHudWorldCanvas)} components and drive one shared HUD from one fire only. "
                        + $"Objects: ‘{a.gameObject.name}’ and ‘{b.gameObject.name}’. "
                        + $"If references are intentional, duplicate the UI under each fire’s hud root, or enable Ignore Shared HUD Graphic Validation on both components.",
                        this);
                    return;
                }
            }
        }

        private bool SharesAnyDrivenGraphicWith(FireDistanceHudWorldCanvas other)
        {
            if (other == null || ReferenceEquals(other, this))
                return false;

            return ReferenceEquals(flameIconImage, other.flameIconImage)
                   || ReferenceEquals(insideBackgroundImage, other.insideBackgroundImage)
                   || ReferenceEquals(outlineAwayImage, other.outlineAwayImage)
                   || ReferenceEquals(outlineReadyImage, other.outlineReadyImage)
                   || ReferenceEquals(outlineCriticalImage, other.outlineCriticalImage)
                   || ReferenceEquals(distanceText, other.distanceText)
                   || ReferenceEquals(messageText, other.messageText);
        }
#endif

        private static void SetTmpColor(TextMeshProUGUI tmp, Color color)
        {
            if (tmp != null)
                tmp.color = color;
        }

        private void EnsureDefaultConfigsIfEmpty()
        {
            if (stateConfigs == null)
                stateConfigs = new List<FireHudStateVisualConfig>();

            if (stateConfigs.Count > 0)
                return;

            stateConfigs.Add(CreateDefaultConfigForState(FireHudDistanceState.Away));
            stateConfigs.Add(CreateDefaultConfigForState(FireHudDistanceState.Ready));
            stateConfigs.Add(CreateDefaultConfigForState(FireHudDistanceState.Critical));
        }

        private void EnsureAllStatesHaveConfig()
        {
            if (stateConfigs == null)
                stateConfigs = new List<FireHudStateVisualConfig>();

            stateConfigs.RemoveAll(static c => c == null);

            if (stateConfigs.Count == 0)
                EnsureDefaultConfigsIfEmpty();

            foreach (FireHudDistanceState s in Enum.GetValues(typeof(FireHudDistanceState)))
            {
                if (FindConfig(s) != null)
                    continue;

                stateConfigs.Add(CreateDefaultConfigForState(s));
            }
        }

        private static FireHudStateVisualConfig CreateDefaultConfigForState(FireHudDistanceState state)
        {
            switch (state)
            {
                case FireHudDistanceState.Away:
                    return new FireHudStateVisualConfig
                    {
                        state = FireHudDistanceState.Away,
                        messageText = "SCANNING ZONE",
                        mainColorA = Color.white,
                        mainColorB = new Color(0.85f, 0.85f, 0.85f),
                        mainFadeSpeed = 2f,
                        flameColorA = new Color(1f, 0.45f, 0f),
                        flameColorB = new Color(1f, 0.92f, 0.2f),
                        flameFadeSpeed = 2f,
                        insideBackgroundColorA = new Color(1f, 1f, 1f, 0.35f),
                        insideBackgroundColorB = new Color(1f, 1f, 1f, 0.08f),
                        insideFadeSpeed = 2f,
                        enableBlink = true
                    };

                case FireHudDistanceState.Ready:
                    return new FireHudStateVisualConfig
                    {
                        state = FireHudDistanceState.Ready,
                        messageText = "READY TO EXTINGUISH",
                        mainColorA = new Color(0.18f, 0.8f, 0.44f),
                        mainColorB = new Color(0.55f, 0.95f, 0.65f),
                        mainFadeSpeed = 2f,
                        flameColorA = new Color(1f, 0.45f, 0f),
                        flameColorB = new Color(1f, 0.92f, 0.2f),
                        flameFadeSpeed = 2f,
                        insideBackgroundColorA = new Color(0.2f, 0.85f, 0.5f, 0.35f),
                        insideBackgroundColorB = new Color(0.45f, 1f, 0.65f, 0.1f),
                        insideFadeSpeed = 2f,
                        enableBlink = true
                    };

                case FireHudDistanceState.Critical:
                default:
                    return new FireHudStateVisualConfig
                    {
                        state = FireHudDistanceState.Critical,
                        messageText = "HEAT ALERT - MOVE BACK",
                        mainColorA = new Color(0.9f, 0.2f, 0.15f),
                        mainColorB = new Color(0.45f, 0.05f, 0.05f),
                        mainFadeSpeed = 2f,
                        flameColorA = new Color(1f, 0.45f, 0f),
                        flameColorB = new Color(1f, 0.15f, 0.1f),
                        flameFadeSpeed = 2f,
                        insideBackgroundColorA = new Color(0.95f, 0.15f, 0.1f, 0.45f),
                        insideBackgroundColorB = new Color(0.35f, 0.02f, 0.02f, 0.12f),
                        insideFadeSpeed = 2f,
                        enableBlink = true
                    };
            }
        }
    }
}
