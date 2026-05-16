using System;
using System.Collections.Generic;
using System.Linq;
using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.Game.Training.FireSelection;
using WoiUtils.AudioSystem;

namespace Woi.Game.Training
{
    /// <summary>
    /// Observes <see cref="ExtinguisherController"/> spray events for a training session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recommended:</b> assign <see cref="PlayerExtinguisherEquipment"/> — the recorder
    /// automatically subscribes to whichever extinguisher is currently equipped (pickup/swap/drop).
    /// </para>
    /// <para>
    /// <b>XR:</b> VR rig’de ayrı bir <see cref="PlayerExtinguisherEquipment"/> (PC’den farklı GameObject) kullanılıyorsa
    /// <see cref="_xrEquipment"/> alanına da atayın; aksi halde <see cref="Woi.Training.FireProximityAnnouncementDriver"/> gibi
    /// yalnızca PC ekipmanına abone kalınır ve VR’da kullanılan tüp tipi sonuç ekranına yansımaz.
    /// </para>
    /// <para>
    /// <b>Legacy:</b> leave both equipment fields empty and assign a single <see cref="ExtinguisherController"/>
    /// (e.g. always-spawned prop).
    /// </para>
    /// <para>
    /// Call <see cref="BeginSession(TrainingSessionBeginContext)"/> (or the string overload) when the scenario starts.
    /// Fire class / required type for CSV are filled from the nearest active <see cref="FireSource"/> at session start unless you set optional overrides below.
    /// Call <see cref="SetSessionEndContext"/> then <see cref="EndSession()"/>, or <see cref="EndSession(TrainingSessionEndContext)"/>,
    /// with outcome data from your simulation / rules.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherSessionRecorder : MonoBehaviour
    {
        [Header("Source (pick one mode)")]
        [Tooltip("PC / paylaşılan: CurrentItem.Controller takibi. XR’da ayrı rig ekipmanı varsa _xrEquipment de doldurun.")]
        [SerializeField] private PlayerExtinguisherEquipment _equipment;

        [Tooltip("XR rig üzerindeki PlayerExtinguisherEquipment (FireProximityAnnouncementDriver._xrExtinguisherEquipment ile aynı referans). Boşsa yalnızca _equipment kullanılır.")]
        [SerializeField] private PlayerExtinguisherEquipment _xrEquipment;

        [Tooltip("Used only when Equipment is not assigned. Fixed single extinguisher.")]
        [SerializeField] private ExtinguisherController _controller;

        [Header("Reporting defaults (company CSV / debrief)")]
        [Tooltip("Merged into every session start so ScenarioDisplayName, FireClass, Required type, and TraineeId are filled even when BeginSession(string) is used.")]
        [SerializeField] private TrainingScenarioReportDefaults _scenarioReportDefaults = new TrainingScenarioReportDefaults();

        [Header("Auto-fill from fire (CSV / debrief)")]
        [Tooltip("Optional override. If empty, FireClass / required type come from the nearest FireSource in the scene at session start.")]
        [SerializeField] private FireData _reportingFireData;

        [Tooltip("Optional override. If empty (and no FireData above), uses nearest FireSource in the scene.")]
        [SerializeField] private FireSource _reportingFireSource;

        [Tooltip("Nearest-fire search uses this position; default is this recorder's transform (use player root if recorder is on a child).")]
        [SerializeField] private Transform _nearestFireReference;

        [Tooltip("If ScenarioDisplayName is still empty after other merges, use this (e.g. lesson title for the spreadsheet).")]
        [SerializeField] private string _scenarioDisplayNameForReport = string.Empty;

        [Header("Sweep feedback (training)")]
        [Tooltip("Rolling-window horizontal span over base-zone hits for trainee debrief.")]
        [SerializeField] private SpraySweepSettings _sweepSettings = new SpraySweepSettings();

        [Header("Scoring")]
        [Tooltip("When FinalScoreOverride is not set, blends technical composite with sweep performance (0–1). Keep low so total score stays closer to aim/coverage/distance. 0 = technical only.")]
        [SerializeField, Range(0f, 1f)] private float _sweepWeightInFinalScore = 0.05f;

        [Header("Debug")]
        [Tooltip("Print the full session report to the console when a session ends.")]
        [SerializeField] private bool _logReportOnEnd = true;

        [Header("Events (SO Overrides)")]
        [Tooltip("SO Event raised when the session officially starts.")]
        [SerializeField] private Obvious.Soap.ScriptableEventNoParam _onSessionStartedSO;
        [Tooltip("SO Event raised when the session formally ends.")]
        [SerializeField] private Obvious.Soap.ScriptableEventNoParam _onSessionEndedSO;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Raised when <see cref="BeginSession(TrainingSessionBeginContext)"/> successfully starts a session.</summary>
        public event Action OnSessionStarted;

        /// <summary>Raised when <see cref="EndSession()"/> is called. Carries the final report.</summary>
        public event Action<SessionReport> OnSessionEnded;

        // ── Session state ─────────────────────────────────────────────────────

        private bool  _sessionActive;
        private float _sessionStartTime;
        private string _sessionId = string.Empty;
        private DateTime _sessionStartedUtc;
        private TrainingSessionBeginContext _beginContext = TrainingSessionBeginContext.Empty;

        private readonly List<TrainingTimelineEvent> _timeline = new();
        private float _firstSprayTime   = -1f;
        private float _currentSprayStartTime = -1f;
        private float _accumulatedSprayDuration;

        private int   _totalEvalTicks;
        private int   _hitTicks;
        private float _totalCoverageScore;
        private float _totalDistanceScore;
        private float _totalExtinguishAmount;
        private bool  _forbiddenAgentUsed;

        private float _capacityCarriedOverNormalized;
        private float _capacityBaseline;

        private readonly Dictionary<SprayMissReason, int> _missReasonCounts = new();

        private ExtinguisherController _subscribedController;

        private readonly SpraySweepTracker _sweepTracker = new SpraySweepTracker();

        /// <summary>Per <see cref="FireSource"/> (instance id): suppression amount by extinguisher type on zone hits.</summary>
        private readonly Dictionary<int, Dictionary<ExtinguisherType, float>> _sprayAmountByFireInstanceId = new();

        /// <summary>Per fire: at least one <see cref="CompatibilityResult.Effective"/> spray tick on that source.</summary>
        private readonly Dictionary<int, bool> _hadEffectiveSprayByFireInstanceId = new();

        /// <summary>Per fire: at least one zone hit (any compatibility).</summary>
        private readonly Dictionary<int, bool> _hadZoneHitByFireInstanceId = new();

        private bool _lastUsedExtinguisherTypeKnown;
        private ExtinguisherType _lastUsedExtinguisherType;

        /// <summary>Optional outcome queued by gameplay; consumed by parameterless <see cref="EndSession()"/>.</summary>
        private TrainingSessionEndContext _pendingEndContext;

        // ── Public state ──────────────────────────────────────────────────────

        public bool IsSessionActive => _sessionActive;

        public ExtinguisherController ActiveController => _subscribedController;

        /// <summary>Live sweep debug while <see cref="IsSessionActive"/> (for <see cref="TrainingSpraySweepDebugHud"/>).</summary>
        public void GetSweepRuntimeDebug(
            out int samplesInWindow,
            out float windowSpanMeters,
            out bool sweepPerformedSession,
            out bool performedLiveWindow,
            out bool sweepRulePassed,
            out float peakSpanMeters,
            out float bestQualityStreakSeconds,
            out int validHitsTotal,
            out int baseHitsTotal)
            => _sweepTracker.GetRuntimeDebug(
                out samplesInWindow,
                out windowSpanMeters,
                out sweepPerformedSession,
                out performedLiveWindow,
                out sweepRulePassed,
                out peakSpanMeters,
                out bestQualityStreakSeconds,
                out validHitsTotal,
                out baseHitsTotal);

        /// <summary>Inspector-tuned thresholds passed to <see cref="SpraySweepTracker"/> each session.</summary>
        public SpraySweepSettings SweepMonitorSettings => _sweepSettings;

        [ContextMenu("Training/Debug/Begin session (Play Mode)")]
        private void ContextDebugBeginSession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSessionRecorder)}] Enter Play Mode, then run this again.", this);
                return;
            }

            BeginSession("debug_session");
            Debug.Log(
                $"[{nameof(ExtinguisherSessionRecorder)}] Debug BeginSession — {nameof(IsSessionActive)}={IsSessionActive}. " +
                "Wire your scenario to call BeginSession for real runs.",
                this);
        }

        [ContextMenu("Training/Debug/End session (Play Mode)")]
        private void ContextDebugEndSession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSessionRecorder)}] Enter Play Mode, then run this again.", this);
                return;
            }

            EndSession();
        }

#if UNITY_EDITOR
        [ContextMenu("Training/Debug/Add Spray Sweep Debug HUD")]
        private void ContextAddSpraySweepDebugHud()
        {
            if (GetComponent<TrainingSpraySweepDebugHud>() != null)
            {
                Debug.Log(
                    $"[{nameof(ExtinguisherSessionRecorder)}] {nameof(TrainingSpraySweepDebugHud)} is already on this GameObject.",
                    this);
                return;
            }

            gameObject.AddComponent<TrainingSpraySweepDebugHud>();
            Debug.Log(
                $"[{nameof(ExtinguisherSessionRecorder)}] Added {nameof(TrainingSpraySweepDebugHud)} " +
                $"(uGUI overlay by default so it draws above UI Toolkit). Keep it on an always-active object.",
                this);
        }
#endif

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            bool anyEquipment = false;

            if (_equipment != null)
            {
                _equipment.OnExtinguisherChanged += HandleEquipmentChanged;
                anyEquipment = true;
            }

            if (_xrEquipment != null && !ReferenceEquals(_xrEquipment, _equipment))
            {
                _xrEquipment.OnExtinguisherChanged += HandleEquipmentChanged;
                anyEquipment = true;
            }

            if (anyEquipment)
                HandleEquipmentChanged(null);
            else if (_controller != null)
                SubscribeController(_controller);
            else
            {
                Debug.LogWarning(
                    $"[{nameof(ExtinguisherSessionRecorder)}] Assign {nameof(PlayerExtinguisherEquipment)} " +
                    $"(and optional XR duplicate) or {nameof(ExtinguisherController)} on {gameObject.name}.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (_sessionActive)
                EndSession();

            if (_equipment != null)
                _equipment.OnExtinguisherChanged -= HandleEquipmentChanged;

            if (_xrEquipment != null && !ReferenceEquals(_xrEquipment, _equipment))
                _xrEquipment.OnExtinguisherChanged -= HandleEquipmentChanged;

            SubscribeController(null);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Starts a session with only a scenario id; merged with <see cref="_scenarioReportDefaults"/>.</summary>
        public void BeginSession(string scenarioId = null)
        {
            var partial = string.IsNullOrEmpty(scenarioId)
                ? TrainingSessionBeginContext.Empty
                : new TrainingSessionBeginContext(scenarioId: scenarioId);
            BeginSession(partial);
        }

        /// <summary>Starts a session; values missing from <paramref name="context"/> are filled from inspector defaults.</summary>
        public void BeginSession(TrainingSessionBeginContext context)
        {
            if (_sessionActive)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSessionRecorder)}] Session already active — call EndSession first.", this);
                return;
            }

            _pendingEndContext          = null;
            _beginContext               = MergeReportingFromFireAndTitle(
                MergeBeginContext(context ?? TrainingSessionBeginContext.Empty, _scenarioReportDefaults));
            _sessionActive              = true;
            _sessionStartTime           = Time.time;
            _sessionId                  = Guid.NewGuid().ToString("N");
            _sessionStartedUtc          = DateTime.UtcNow;
            _lastUsedExtinguisherTypeKnown = false;
            _lastUsedExtinguisherType      = default;
            _timeline.Clear();
            _timeline.Add(new TrainingTimelineEvent(0f, TrainingTimelineEventKind.SessionStarted));
            _firstSprayTime             = -1f;
            _currentSprayStartTime      = -1f;
            _accumulatedSprayDuration   = 0f;
            _totalEvalTicks             = 0;
            _hitTicks                   = 0;
            _totalCoverageScore         = 0f;
            _totalDistanceScore         = 0f;
            _totalExtinguishAmount      = 0f;
            _capacityCarriedOverNormalized = 0f;
            _capacityBaseline              = _subscribedController != null
                ? _subscribedController.NormalizedCapacity
                : 1f;
            _forbiddenAgentUsed         = false;
            _missReasonCounts.Clear();

            _sprayAmountByFireInstanceId.Clear();
            _hadEffectiveSprayByFireInstanceId.Clear();
            _hadZoneHitByFireInstanceId.Clear();

            _sweepTracker.Reset(_sweepSettings);

            CaptureUsedExtinguisherFrom(_subscribedController);

            _onSessionStartedSO?.Raise();
            OnSessionStarted?.Invoke();
        }

        /// <summary>
        /// Queues outcome data for the next parameterless <see cref="EndSession()"/> (e.g. from your scenario manager).
        /// </summary>
        public void SetSessionEndContext(TrainingSessionEndContext endContext)
        {
            _pendingEndContext = endContext ?? TrainingSessionEndContext.Empty;
        }

        /// <summary>Ends the session using <see cref="SetSessionEndContext"/> if set, otherwise empty end data.</summary>
        public SessionReport EndSession()
        {
            TrainingSessionEndContext end = _pendingEndContext ?? TrainingSessionEndContext.Empty;
            _pendingEndContext = null;
            return EndSession(end);
        }

        /// <summary>Ends the session with explicit outcome; clears any pending end context.</summary>
        public SessionReport EndSession(TrainingSessionEndContext endContext)
        {
            _pendingEndContext = null;
            return EndSessionCore(endContext ?? TrainingSessionEndContext.Empty);
        }

        private SessionReport EndSessionCore(TrainingSessionEndContext endContext)
        {
            if (!_sessionActive)
            {
                Debug.LogWarning($"[{nameof(ExtinguisherSessionRecorder)}] No active session to end.", this);
                return null;
            }

            if (_currentSprayStartTime >= 0f)
            {
                _accumulatedSprayDuration += Time.time - _currentSprayStartTime;
                _currentSprayStartTime     = -1f;
            }

            _sessionActive = false;

            _timeline.Add(new TrainingTimelineEvent(
                Time.time - _sessionStartTime,
                TrainingTimelineEventKind.SessionEnded));

            CaptureUsedExtinguisherFrom(_subscribedController);

            SessionReport report = BuildReport(endContext);

            if (_logReportOnEnd)
                Debug.Log(report.ToString(), this);

            StopAllWoiAudioBeforeSessionEndedEvent();

            _onSessionEndedSO?.Raise();
            OnSessionEnded?.Invoke(report);
            return report;
        }

        /// <summary>
        /// Clears Woi <see cref="AudioSystem"/> playback so gameplay VO/SFX do not overlap the results screen;
        /// runs immediately before <see cref="_onSessionEndedSO"/> and <see cref="OnSessionEnded"/>.
        /// </summary>
        static void StopAllWoiAudioBeforeSessionEndedEvent()
        {
            if (AudioSystem.TryGetFromServiceLocator(out AudioSystem registered) && registered != null)
            {
                registered.StopAll();
                return;
            }

            AudioSystem fallback = UnityEngine.Object.FindFirstObjectByType<AudioSystem>();
            if (fallback != null)
                fallback.StopAll();
        }

        /// <summary>Live snapshot without ending. Uses empty end context (not evaluated / fire not out).</summary>
        public SessionReport GetPartialReport()
        {
            if (!_sessionActive) return null;
            return BuildReport(TrainingSessionEndContext.Empty);
        }

        // ── Equipment / controller binding ─────────────────────────────────────

        private void HandleEquipmentChanged(ExtinguisherPickupItem _)
        {
            ExtinguisherPickupItem tracked = ResolveTrackedEquippedItem();
            SubscribeController(tracked != null ? tracked.Controller : null);
        }

        /// <summary>
        /// PC sahnesinde öncelik <see cref="_equipment"/>; XR’da VR rig’deki <see cref="_xrEquipment"/> (doluyse).
        /// </summary>
        ExtinguisherPickupItem ResolveTrackedEquippedItem()
        {
            if (FirePlatformRuntime.IsVR)
            {
                if (_xrEquipment != null && _xrEquipment.CurrentItem != null)
                    return _xrEquipment.CurrentItem;
                if (_equipment != null && _equipment.CurrentItem != null)
                    return _equipment.CurrentItem;
                return null;
            }

            if (_equipment != null && _equipment.CurrentItem != null)
                return _equipment.CurrentItem;
            if (_xrEquipment != null && _xrEquipment.CurrentItem != null)
                return _xrEquipment.CurrentItem;
            return null;
        }

        private void SubscribeController(ExtinguisherController next)
        {
            if (next == _subscribedController)
                return;

            UnsubscribeController(_subscribedController);

            _subscribedController = next;

            if (_subscribedController == null)
                return;

            _subscribedController.OnSprayStarted   += HandleSprayStarted;
            _subscribedController.OnSprayStopped   += HandleSprayStopped;
            _subscribedController.OnSprayEvaluated += HandleSprayEvaluated;

            if (_sessionActive)
                _capacityBaseline = _subscribedController.NormalizedCapacity;

            CaptureUsedExtinguisherFrom(_subscribedController);
        }

        private void UnsubscribeController(ExtinguisherController ctrl)
        {
            if (ctrl == null)
                return;

            if (_sessionActive)
                _capacityCarriedOverNormalized += Mathf.Clamp01(_capacityBaseline - ctrl.NormalizedCapacity);

            ctrl.OnSprayStarted   -= HandleSprayStarted;
            ctrl.OnSprayStopped   -= HandleSprayStopped;
            ctrl.OnSprayEvaluated -= HandleSprayEvaluated;

            if (_subscribedController == ctrl)
                _subscribedController = null;
        }

        private void CaptureUsedExtinguisherFrom(ExtinguisherController ctrl)
        {
            if (ctrl == null || ctrl.ExtinguisherData == null)
                return;

            _lastUsedExtinguisherTypeKnown = true;
            _lastUsedExtinguisherType      = ctrl.ExtinguisherData.ExtinguisherType;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleSprayStarted()
        {
            if (!_sessionActive) return;

            _currentSprayStartTime = Time.time;

            if (_firstSprayTime < 0f)
                _firstSprayTime = Time.time;

            _timeline.Add(new TrainingTimelineEvent(
                Time.time - _sessionStartTime,
                TrainingTimelineEventKind.SprayStarted));
        }

        private void HandleSprayStopped()
        {
            if (!_sessionActive || _currentSprayStartTime < 0f) return;

            _accumulatedSprayDuration += Time.time - _currentSprayStartTime;
            _currentSprayStartTime     = -1f;

            _timeline.Add(new TrainingTimelineEvent(
                Time.time - _sessionStartTime,
                TrainingTimelineEventKind.SprayStopped));
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (!_sessionActive) return;

            _totalEvalTicks++;

            if (result.DidHitZone)
            {
                _hitTicks++;
                _totalCoverageScore    += result.CoverageScore;
                _totalDistanceScore    += result.DistanceScore;
                _totalExtinguishAmount += result.ExtinguishAmountCalculated;

                _sweepTracker.RecordHit(Time.time, result);

                FireSource hitSource = result.Source;
                if (hitSource != null)
                {
                    int fireId = hitSource.GetInstanceID();
                    _hadZoneHitByFireInstanceId[fireId] = true;

                    if (result.Compatibility == CompatibilityResult.Effective)
                        _hadEffectiveSprayByFireInstanceId[fireId] = true;

                    if (_subscribedController != null && _subscribedController.ExtinguisherData != null)
                    {
                        ExtinguisherType sprayType = _subscribedController.ExtinguisherData.ExtinguisherType;
                        if (!_sprayAmountByFireInstanceId.TryGetValue(fireId, out Dictionary<ExtinguisherType, float> byType))
                        {
                            byType = new Dictionary<ExtinguisherType, float>();
                            _sprayAmountByFireInstanceId[fireId] = byType;
                        }

                        byType.TryGetValue(sprayType, out float prev);
                        byType[sprayType] = prev + result.ExtinguishAmountCalculated;
                    }
                }

                if (result.Compatibility == CompatibilityResult.Forbidden)
                    _forbiddenAgentUsed = true;
            }
            else
            {
                _missReasonCounts.TryGetValue(result.MissReason, out int count);
                _missReasonCounts[result.MissReason] = count + 1;
            }
        }

        // ── Report builder ────────────────────────────────────────────────────

        private static TrainingSessionBeginContext MergeBeginContext(
            TrainingSessionBeginContext c,
            TrainingScenarioReportDefaults d)
        {
            if (d == null)
                return c;

            string scenarioId = !string.IsNullOrEmpty(c.ScenarioId) ? c.ScenarioId : d.DefaultScenarioId ?? string.Empty;
            string display    = !string.IsNullOrEmpty(c.ScenarioDisplayName) ? c.ScenarioDisplayName : d.ScenarioDisplayName ?? string.Empty;
            string trainee    = !string.IsNullOrEmpty(c.TraineeId) ? c.TraineeId : d.DefaultTraineeId ?? string.Empty;

            bool hasFire = c.HasFireClass || d.SpecifyFireClass;
            FireClass fireClass = c.HasFireClass ? c.FireClass : d.FireClass;

            bool hasRequired = c.HasRequiredExtinguisherType || d.SpecifyRequiredExtinguisherType;
            ExtinguisherType req = c.HasRequiredExtinguisherType ? c.RequiredExtinguisherType : d.RequiredExtinguisherType;

            return new TrainingSessionBeginContext(
                traineeId: trainee,
                scenarioId: scenarioId,
                scenarioDisplayName: display,
                hasFireClass: hasFire,
                fireClass: fireClass,
                hasRequiredExtinguisherType: hasRequired,
                requiredExtinguisherType: req);
        }

        /// <summary>
        /// Fills scenario display name, <see cref="FireClass"/>, and required extinguisher from <see cref="FireData"/>.
        /// Resolution order: begin context → defaults → optional overrides → nearest in-scene <see cref="FireSource"/>.
        /// </summary>
        private TrainingSessionBeginContext MergeReportingFromFireAndTitle(TrainingSessionBeginContext c)
        {
            FireData fd = ResolveReportingFireData();

            string display = c.ScenarioDisplayName;
            if (string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(_scenarioDisplayNameForReport))
                display = _scenarioDisplayNameForReport;
            if (string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(c.ScenarioId))
                display = c.ScenarioId;
            if (string.IsNullOrEmpty(display))
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(sceneName))
                    display = sceneName;
            }

            bool hasFire = c.HasFireClass;
            FireClass fireClass = c.FireClass;
            if (!hasFire && fd != null)
            {
                hasFire   = true;
                fireClass = fd.FireClass;
            }

            bool hasReq = c.HasRequiredExtinguisherType;
            ExtinguisherType req = c.RequiredExtinguisherType;
            if (!hasReq && fd != null)
            {
                ExtinguisherType[] allowed = fd.AllowedExtinguisherTypes;
                if (allowed != null && allowed.Length > 0)
                {
                    hasReq = true;
                    req    = allowed[0];
                }
            }

            return new TrainingSessionBeginContext(
                traineeId: c.TraineeId,
                scenarioId: c.ScenarioId,
                scenarioDisplayName: display,
                hasFireClass: hasFire,
                fireClass: fireClass,
                hasRequiredExtinguisherType: hasReq,
                requiredExtinguisherType: req);
        }

        /// <summary>
        /// Manual FireData / FireSource override, else nearest active (non-extinguished) <see cref="FireSource"/> with data,
        /// else nearest source of any state (so reports still work after the fire is out).
        /// </summary>
        private FireData ResolveReportingFireData()
        {
            if (_reportingFireData != null)
                return _reportingFireData;
            if (_reportingFireSource != null && _reportingFireSource.Data != null)
                return _reportingFireSource.Data;

            Vector3 reference = _nearestFireReference != null ? _nearestFireReference.position : transform.position;

            FireSource[] all = FindObjectsByType<FireSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            FireSource bestActive = null;
            float bestActiveDistSq = float.MaxValue;
            FireSource bestAny = null;
            float bestAnyDistSq = float.MaxValue;

            foreach (FireSource fs in all)
            {
                if (fs == null || fs.Data == null)
                    continue;
                if (!TrainingFireSelectionQueries.IsIncludedInTrainingSession(fs))
                    continue;

                float d = (fs.transform.position - reference).sqrMagnitude;
                if (d < bestAnyDistSq)
                {
                    bestAnyDistSq = d;
                    bestAny       = fs;
                }

                if (fs.State != FireSourceState.Extinguished)
                {
                    if (d < bestActiveDistSq)
                    {
                        bestActiveDistSq = d;
                        bestActive       = fs;
                    }
                }
            }

            FireSource chosen = bestActive != null ? bestActive : bestAny;
            return chosen != null ? chosen.Data : null;
        }

        /// <summary>
        /// True when at least one fire had spray contact and every such fire is fully extinguished
        /// (used for debrief messages only).
        /// </summary>
        static bool AllSprayContactFiresFullyOut(IReadOnlyList<TrainingFireInstanceReport> fireRows)
        {
            if (fireRows == null || fireRows.Count == 0)
                return false;

            bool anyContact = false;
            foreach (TrainingFireInstanceReport f in fireRows)
            {
                if (!f.HadSprayContactOnThisFire)
                    continue;
                anyContact = true;
                if (!f.FireFullyExtinguished)
                    return false;
            }

            return anyContact;
        }

        private SessionReport BuildReport(TrainingSessionEndContext end)
        {
            float now             = Time.time;
            float sessionDuration = now - _sessionStartTime;

            float sprayDuration = _accumulatedSprayDuration;
            if (_currentSprayStartTime >= 0f)
                sprayDuration += now - _currentSprayStartTime;

            float timeToFirstSpray = _firstSprayTime >= 0f
                ? _firstSprayTime - _sessionStartTime
                : -1f;

            float aimAccuracy = _totalEvalTicks > 0
                ? (float)_hitTicks / _totalEvalTicks
                : 0f;

            float avgCoverage = _hitTicks > 0
                ? _totalCoverageScore / _hitTicks
                : 0f;

            float avgDistance = _hitTicks > 0
                ? _totalDistanceScore / _hitTicks
                : 0f;

            float currentSegmentUsed = _subscribedController != null
                ? Mathf.Clamp01(_capacityBaseline - _subscribedController.NormalizedCapacity)
                : 0f;

            float capacityUsed = Mathf.Clamp01(_capacityCarriedOverNormalized + currentSegmentUsed);

            var missBreakdown = new TrainingMissBreakdown(new Dictionary<SprayMissReason, int>(_missReasonCounts));

            var technical = new TrainingTechnicalMetrics(
                totalSprayDurationSeconds: sprayDuration,
                timeToFirstSpraySeconds:   timeToFirstSpray,
                totalEvalTicks:            _totalEvalTicks,
                hitTicks:                  _hitTicks,
                aimAccuracy:               aimAccuracy,
                avgCoverageScore:          avgCoverage,
                avgDistanceScore:          avgDistance,
                totalExtinguishAmount:     _totalExtinguishAmount,
                normalizedCapacityUsed:    capacityUsed,
                forbiddenAgentUsed:        _forbiddenAgentUsed,
                missBreakdown:             missBreakdown);

            TrainingSweepMetrics sweep = _sweepTracker.BuildFinalMetrics();

            TrainingRuleOutcome rules = end.RuleOutcome ?? TrainingRuleOutcome.Pending();

            bool hasUsed = _lastUsedExtinguisherTypeKnown;
            ExtinguisherType usedType = _lastUsedExtinguisherType;

            List<TrainingFireInstanceReport> fireRows = BuildAllFireInstanceReports();

            bool hasReq = _beginContext.HasRequiredExtinguisherType;
            bool correctType;
            bool fireAllOut;
            bool wrongTypeSession;
            bool usedUnknownWithRequirement;

            if (fireRows.Count > 0)
            {
                correctType = fireRows.TrueForAll(f => f.CorrectExtinguisherSelected);
                fireAllOut  = fireRows.TrueForAll(f => f.FireFullyExtinguished);
                wrongTypeSession = fireRows.Any(f =>
                    f.HasAllowedExtinguisherTypes && f.HadSprayContactOnThisFire && !f.CorrectExtinguisherSelected);
                usedUnknownWithRequirement =
                    (!hasUsed && fireRows.Any(f => f.HasAllowedExtinguisherTypes))
                    || fireRows.Any(f =>
                        f.HasAllowedExtinguisherTypes
                        && f.HadSprayContactOnThisFire
                        && !f.HasUsedExtinguisherTypeOnThisFire);
            }
            else
            {
                correctType = hasReq && hasUsed && _beginContext.RequiredExtinguisherType == usedType;
                wrongTypeSession = hasReq && hasUsed && _beginContext.RequiredExtinguisherType != usedType;
                usedUnknownWithRequirement = hasReq && !hasUsed;
                fireAllOut = end.FireFullyExtinguished;
            }

            bool tubeEmptyAtEnd = _subscribedController != null && _subscribedController.IsDepleted;

            bool? depletedOverride = end.ExtinguisherDepletedBeforeCompletion;
            bool depletedBefore = depletedOverride.HasValue ? depletedOverride.Value : tubeEmptyAtEnd;

            bool sprayContactFiresAllOut = AllSprayContactFiresFullyOut(fireRows);
            bool firesStillIncompleteForDebrief = !fireAllOut && !sprayContactFiresAllOut;
            bool depletionMistakeForDebrief = firesStillIncompleteForDebrief
                && (tubeEmptyAtEnd || capacityUsed >= 0.995f);

            List<string> critical = BuildCriticalMistakes(
                end, rules, _forbiddenAgentUsed, wrongTypeSession, depletionMistakeForDebrief, usedUnknownWithRequirement, fireRows, sweep);

            bool rulesEvaluated = rules.WasEvaluated;
            bool? overallPass = rulesEvaluated ? rules.Passed : (bool?)null;

            float techScore = technical.CompositeProficiencyScore;
            float sweepScore01 = sweep.SweepPerformanceScore;
            float finalScore = end.FinalScoreOverride.HasValue
                ? Mathf.Clamp01(end.FinalScoreOverride.Value)
                : _sweepWeightInFinalScore <= 0f
                    ? techScore
                    : Mathf.Clamp01(techScore * (1f - _sweepWeightInFinalScore) + sweepScore01 * _sweepWeightInFinalScore);

            DateTime endedUtc = DateTime.UtcNow;
            var client = new TrainingClientSummary(
                sessionId:                  _sessionId,
                traineeId:                  _beginContext.TraineeId,
                scenarioId:                 _beginContext.ScenarioId,
                scenarioDisplayName:        _beginContext.ScenarioDisplayName,
                startedUtcIso8601:          _sessionStartedUtc.ToString("o"),
                endedUtcIso8601:            endedUtc.ToString("o"),
                sessionDurationSeconds:     sessionDuration,
                timeToFirstResponseSeconds: timeToFirstSpray,
                hasFireClass:               fireRows.Count == 1 || _beginContext.HasFireClass,
                fireClass:                  fireRows.Count == 1 ? fireRows[0].FireClass : _beginContext.FireClass,
                hasRequiredExtinguisherType: fireRows.Count == 1
                    ? fireRows[0].HasAllowedExtinguisherTypes
                    : _beginContext.HasRequiredExtinguisherType,
                requiredExtinguisherType:   fireRows.Count == 1 && fireRows[0].HasAllowedExtinguisherTypes
                    ? fireRows[0].RepresentativeRequiredExtinguisherType
                    : _beginContext.RequiredExtinguisherType,
                hasUsedExtinguisherType:    _lastUsedExtinguisherTypeKnown,
                usedExtinguisherType:       _lastUsedExtinguisherType,
                correctExtinguisherSelected: correctType,
                fireFullyExtinguished:      fireAllOut,
                extinguisherDepletedBeforeCompletion: depletedBefore,
                overallTrainingPassed:      overallPass,
                rulesEvaluated:             rulesEvaluated,
                finalScore:                 finalScore,
                sweepPerformanceScore:      sweepScore01,
                criticalMistakes:           critical);

            return new SessionReport(
                client,
                rules,
                technical,
                sweep,
                fireRows,
                new List<TrainingTimelineEvent>(_timeline));
        }

        private List<TrainingFireInstanceReport> BuildAllFireInstanceReports()
        {
            FireSource[] all = FindObjectsByType<FireSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var ordered = all
                .Where(fs => fs != null && fs.Data != null
                    && TrainingFireSelectionQueries.IsIncludedInTrainingSession(fs))
                .OrderBy(fs => fs.gameObject.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(fs => fs.GetInstanceID())
                .ToList();

            var list = new List<TrainingFireInstanceReport>(ordered.Count);
            foreach (FireSource fs in ordered)
            {
                FireData fd = fs.Data;
                ExtinguisherType[] allowed = fd.AllowedExtinguisherTypes;
                bool hasAllowed = allowed != null && allowed.Length > 0;
                string display = TrainingReportLabels.JoinAllowedExtinguishers(allowed);
                ExtinguisherType rep = hasAllowed ? allowed[0] : default;

                int fireId = fs.GetInstanceID();
                bool hadContact = _hadZoneHitByFireInstanceId.ContainsKey(fireId);

                bool hasLookup = _sprayAmountByFireInstanceId.TryGetValue(fireId, out Dictionary<ExtinguisherType, float> byType);
                bool hasUsedOnFire = hadContact && hasLookup && byType != null && byType.Count > 0;

                ExtinguisherType dominantUsed = default;
                if (hasUsedOnFire)
                {
                    float bestAmount = -1f;
                    foreach (KeyValuePair<ExtinguisherType, float> kv in byType)
                    {
                        if (kv.Value > bestAmount)
                        {
                            bestAmount = kv.Value;
                            dominantUsed = kv.Key;
                        }
                    }
                }

                bool hadEffectiveSpray = _hadEffectiveSprayByFireInstanceId.ContainsKey(fireId);

                bool correct;
                if (!hasAllowed)
                    correct = true;
                else if (!hadContact)
                    correct = true;
                else
                    correct = hadEffectiveSpray;

                string key = string.IsNullOrEmpty(fs.gameObject.name)
                    ? $"{fd.FireClass}#{fireId}"
                    : $"{fs.gameObject.name}#{fireId}";

                list.Add(new TrainingFireInstanceReport(
                    key,
                    fd.FireClass,
                    display,
                    hasAllowed,
                    rep,
                    hadContact,
                    hasUsedOnFire,
                    dominantUsed,
                    correct,
                    fs.IsExtinguished));
            }

            return list;
        }

        private static List<string> BuildCriticalMistakes(
            TrainingSessionEndContext end,
            TrainingRuleOutcome rules,
            bool forbiddenAgentUsed,
            bool wrongExtinguisherType,
            bool addDepletionBeforeFireOutMistake,
            bool usedTypeUnknownWhenRequired,
            List<TrainingFireInstanceReport> fireRows,
            TrainingSweepMetrics sweep)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            void add(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                s = s.Trim();
                if (seen.Add(s))
                    list.Add(s);
            }

            foreach (string x in end.ExtraCriticalMistakes)
                add(x);

            if (usedTypeUnknownWhenRequired)
                add("Used extinguisher type was not recorded; equip an extinguisher before ending the session.");

            if (rules.WasEvaluated && !rules.Passed)
            {
                foreach (string f in rules.FailureReasons)
                    add(f);
            }

            if (forbiddenAgentUsed)
                add("Incompatible extinguisher agent sprayed on fire zone.");

            if (wrongExtinguisherType)
                add("Wrong extinguisher type for one or more fires.");

            if (addDepletionBeforeFireOutMistake)
                add("Extinguisher was depleted before the fire was fully extinguished.");

            if (fireRows != null && fireRows.Count > 0)
            {
                foreach (TrainingFireInstanceReport f in fireRows)
                {
                    if (!f.FireFullyExtinguished)
                        add($"Fire '{f.FireSourceKey}' was not fully extinguished.");
                }
            }
            else if (!end.FireFullyExtinguished)
                add("Fire was not fully extinguished.");

            if (sweep != null && sweep.ValidHitsTotal > 0)
            {
                int sweepPct = Mathf.Clamp(Mathf.RoundToInt(sweep.SweepPerformanceScore * 100f), 0, 100);
                add($"Sweep performance: {sweepPct}/100 — {sweep.SweepFeedbackText}");
            }

            return list;
        }
    }
}
