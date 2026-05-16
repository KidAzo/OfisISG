using System;
using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Game.Training
{
    /// <summary>
    /// Tunable parameters for <see cref="SpraySweepTracker"/>.
    /// </summary>
    [Serializable]
    public sealed class SpraySweepSettings
    {
        [Tooltip("How far back (seconds) to look for hit samples when measuring horizontal span.")]
        [Min(0.05f)] public float RollingWindowSeconds = 1f;

        [Tooltip("Minimum number of valid hits required inside the window before span is trusted.")]
        [Min(2)] public int MinimumSampleCount = 4;

        [Tooltip("Minimum horizontal span (meters, XZ plane) between hit points for a wide motion.")]
        [Min(0.01f)] public float MinimumHorizontalSpanMeters = 0.3f;

        [Tooltip(
            "Loose \"sweep performed\" gate: oldest→newest sample in the rolling window must be separated by at least this many seconds. " +
            "Stops a single burst of hits (or one-frame noise) from maxing pairwise span. Set 0 to disable (not recommended).")]
        [Min(0f)] public float MinimumPerformedTemporalSpreadSeconds = 0.15f;

        [Tooltip(
            "Minimum wall-clock time the trainee must sustain \"quality\" motion (span + samples + temporal spread). " +
            "Streak resets on any evaluation that fails the quality gate.")]
        [Min(0.05f)] public float MinimumSweepDurationSeconds = 0.65f;

        [Tooltip(
            "Oldest→newest sample time inside the window must cover at least this fraction of the rolling window. " +
            "Stops instant clusters (e.g. two quick taps) from counting as quality motion.")]
        [Range(0.05f, 1f)] public float MinimumTemporalSpreadFraction = 0.38f;

        [Tooltip(
            "Mean XZ distance from the hit centroid must be at least this fraction of the max pairwise span. " +
            "0 = disabled. Catches two outlier points with the rest stacked in the middle.")]
        [Range(0f, 1f)] public float MinimumMeanSpreadFromCentroidFraction = 0.22f;

        [Tooltip("If true, only hits on FireTargetZone with ZoneType = Base are used.")]
        public bool BaseZoneHitsOnly = true;
    }

    /// <summary>
    /// Session outcome for horizontal sweep-at-base feedback (training layer only).
    /// </summary>
    public sealed class TrainingSweepMetrics
    {
        /// <summary>True if loose sweep motion (span + sample count + performed temporal spread) latched at least once this session.</summary>
        public bool SweepPerformed { get; }

        /// <summary>True if motion also met duration and quality gates (training pass).</summary>
        public bool SweepRulePassed { get; }

        /// <summary>Peak horizontal span (meters, XZ) from the best qualifying window, else best \"performed\" window.</summary>
        public float SweepCoverageWidth { get; }

        /// <summary>Longest continuous run of quality evaluations (seconds).</summary>
        public float SweepDurationSeconds { get; }

        public string SweepFeedbackText { get; }

        public int ValidHitsTotal { get; }

        public int BaseHitsTotal { get; }

        /// <summary>0–1 training score from sweep behaviour (explained in <see cref="SpraySweepTracker"/>).</summary>
        public float SweepPerformanceScore { get; }

        public TrainingSweepMetrics(
            bool sweepPerformed,
            bool sweepRulePassed,
            float sweepCoverageWidth,
            float sweepDurationSeconds,
            float sweepPerformanceScore,
            string sweepFeedbackText,
            int validHitsTotal,
            int baseHitsTotal)
        {
            SweepPerformed         = sweepPerformed;
            SweepRulePassed        = sweepRulePassed;
            SweepCoverageWidth     = sweepCoverageWidth;
            SweepDurationSeconds   = sweepDurationSeconds;
            SweepPerformanceScore  = Mathf.Clamp01(sweepPerformanceScore);
            SweepFeedbackText    = sweepFeedbackText ?? string.Empty;
            ValidHitsTotal         = validHitsTotal;
            BaseHitsTotal          = baseHitsTotal;
        }

        public static TrainingSweepMetrics Empty { get; } = new TrainingSweepMetrics(
            sweepPerformed: false,
            sweepRulePassed: false,
            sweepCoverageWidth: 0f,
            sweepDurationSeconds: 0f,
            sweepPerformanceScore: 0f,
            sweepFeedbackText: "No spray hits recorded.",
            validHitsTotal: 0,
            baseHitsTotal: 0);

        public void AppendSweepText(System.Text.StringBuilder sb)
        {
            sb.AppendLine("  --- Sweep (base, horizontal) ---");
            sb.AppendLine($"  Sweep performed      : {SweepPerformed}");
            sb.AppendLine($"  Rule passed          : {SweepRulePassed}");
            sb.AppendLine($"  Peak span (XZ)       : {SweepCoverageWidth:F2} m");
            sb.AppendLine($"  Quality streak (max) : {SweepDurationSeconds:F2} s");
            sb.AppendLine($"  Performance score    : {SweepPerformanceScore:P0}");
            sb.AppendLine($"  Valid / base hits    : {ValidHitsTotal} / {BaseHitsTotal}");
            sb.AppendLine($"  Feedback             : {SweepFeedbackText}");
        }
    }

    /// <summary>
    /// Rolling-window horizontal span with separate \"performed\" vs \"rule passed\" training gates.
    /// </summary>
    /// <remarks>
    /// <b>Performed</b> (loose): max pairwise XZ span and sample count exceed thresholds — quick flicks can qualify.<br />
    /// <b>Rule passed</b> (strict): same span/count plus samples spread in time, optional centroid spread,
    /// and the quality state sustained for <see cref="SpraySweepSettings.MinimumSweepDurationSeconds"/>.
    /// </remarks>
    public sealed class SpraySweepTracker
    {
        private SpraySweepSettings _settings = new SpraySweepSettings();

        private readonly List<Vector3> _scratch = new List<Vector3>(64);

        private struct Sample
        {
            public float Time;
            public float X;
            public float Z;
        }

        private readonly List<Sample> _samples = new List<Sample>(128);

        private bool _sweepPerformed;
        private bool _sweepRulePassed;
        private float _peakSpanPerformed;
        private float _peakSpanQuality;
        private float _maxQualityStreakSeconds;
        private int _validHitsTotal;
        private int _baseHitsTotal;

        /// <summary>Hits that landed while the rolling window satisfied the strict quality gate (for score consistency).</summary>
        private int _hitsDuringQualityWindow;

        private bool _qualityStreakActive;
        private float _qualityStreakStartTime;
        private float _lastQualityTime;
        private bool _anyQualitySnapshot;

        private float _lastHitTime;

        /// <summary>
        /// Latches to <c>true</c> once strict sweep quality is sustained (see <see cref="SpraySweepSettings.MinimumSweepDurationSeconds"/>).
        /// Resets with <see cref="Reset"/>.
        /// </summary>
        public bool IsSweepRulePassed => _sweepRulePassed;

        public void Reset(SpraySweepSettings settings = null)
        {
            _settings = settings ?? new SpraySweepSettings();
            _samples.Clear();
            _sweepPerformed = false;
            _sweepRulePassed = false;
            _peakSpanPerformed = 0f;
            _peakSpanQuality = 0f;
            _maxQualityStreakSeconds = 0f;
            _validHitsTotal = 0;
            _baseHitsTotal = 0;
            _hitsDuringQualityWindow = 0;
            _qualityStreakActive = false;
            _qualityStreakStartTime = 0f;
            _lastQualityTime = 0f;
            _anyQualitySnapshot = false;
            _lastHitTime = 0f;
        }

        /// <summary>Call for each evaluator tick while a session is active.</summary>
        public void RecordHit(float time, in ExtinguishResult result)
        {
            if (!result.DidHitZone)
                return;

            _lastHitTime = time;
            _validHitsTotal++;

            bool isBase = result.HitZone != null && result.HitZone.ZoneType == FireZoneType.Base;
            if (isBase)
                _baseHitsTotal++;

            if (_settings.BaseZoneHitsOnly && !isBase)
                return;

            Vector3 p = result.HitPoint;
            _samples.Add(new Sample { Time = time, X = p.x, Z = p.z });

            float cutoff = time - _settings.RollingWindowSeconds;
            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                if (_samples[i].Time < cutoff)
                    _samples.RemoveAt(i);
            }

            EvaluateWindow(time, out bool qualityAtThisHit);
            if (qualityAtThisHit)
                _hitsDuringQualityWindow++;
        }

        private void EvaluateWindow(float time, out bool qualityWindowAtThisHit)
        {
            qualityWindowAtThisHit = false;
            int n = _samples.Count;
            if (n < _settings.MinimumSampleCount)
            {
                CloseQualityStreak(time);
                return;
            }

            float span = ComputeMaxPairwiseDistanceXZ(_samples, _scratch);
            float tMin = float.MaxValue;
            float tMax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float t = _samples[i].Time;
                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }

            float temporalSpread = tMax - tMin;
            float minTemporalRequired = Mathf.Max(
                0.01f,
                _settings.RollingWindowSeconds * _settings.MinimumTemporalSpreadFraction);

            bool performedTemporalOk = _settings.MinimumPerformedTemporalSpreadSeconds <= 1e-6f
                || temporalSpread >= _settings.MinimumPerformedTemporalSpreadSeconds;
            bool performedNow = span >= _settings.MinimumHorizontalSpanMeters && performedTemporalOk;
            if (performedNow)
            {
                _sweepPerformed = true;
                if (span > _peakSpanPerformed)
                    _peakSpanPerformed = span;
            }

            bool temporalOk = temporalSpread >= minTemporalRequired;
            bool meanOk = MeanSpreadGateOk(_samples, span);

            bool qualityNow = performedNow && temporalOk && meanOk;
            qualityWindowAtThisHit = qualityNow;
            if (qualityNow)
            {
                _anyQualitySnapshot = true;
                if (span > _peakSpanQuality)
                    _peakSpanQuality = span;
            }

            if (qualityNow)
            {
                if (!_qualityStreakActive)
                {
                    _qualityStreakActive = true;
                    _qualityStreakStartTime = time;
                }

                _lastQualityTime = time;
                float streakLen = time - _qualityStreakStartTime;
                if (streakLen >= _settings.MinimumSweepDurationSeconds)
                    _sweepRulePassed = true;
            }
            else
            {
                CloseQualityStreak(time);
            }
        }

        private void CloseQualityStreak(float time)
        {
            if (!_qualityStreakActive)
                return;

            float endedDuration = _lastQualityTime - _qualityStreakStartTime;
            if (endedDuration > _maxQualityStreakSeconds)
                _maxQualityStreakSeconds = endedDuration;

            _qualityStreakActive = false;
        }

        private bool MeanSpreadGateOk(List<Sample> samples, float maxPairwiseSpan)
        {
            float f = _settings.MinimumMeanSpreadFromCentroidFraction;
            if (f <= 0.0001f || maxPairwiseSpan < 0.001f)
                return true;

            int n = samples.Count;
            double sx = 0, sz = 0;
            for (int i = 0; i < n; i++)
            {
                sx += samples[i].X;
                sz += samples[i].Z;
            }

            float cx = (float)(sx / n);
            float cz = (float)(sz / n);
            double sumDist = 0;
            for (int i = 0; i < n; i++)
            {
                float dx = samples[i].X - cx;
                float dz = samples[i].Z - cz;
                sumDist += Math.Sqrt(dx * dx + dz * dz);
            }

            float meanDist = (float)(sumDist / n);
            return meanDist >= maxPairwiseSpan * f;
        }

        public TrainingSweepMetrics BuildFinalMetrics()
        {
            if (_qualityStreakActive && _lastQualityTime >= _qualityStreakStartTime)
            {
                float open = _lastQualityTime - _qualityStreakStartTime;
                if (open > _maxQualityStreakSeconds)
                    _maxQualityStreakSeconds = open;
            }

            float width = _peakSpanQuality > 0.0001f ? _peakSpanQuality : _peakSpanPerformed;
            float duration = _maxQualityStreakSeconds;

            string feedback = BuildFeedback();
            float perf = ComputeSweepPerformanceScore01(width, duration);

            return new TrainingSweepMetrics(
                _sweepPerformed,
                _sweepRulePassed,
                width,
                duration,
                perf,
                feedback,
                _validHitsTotal,
                _baseHitsTotal);
        }

        /// <summary>
        /// 0–1 score, intentionally strict vs technical composite: quality hit share weighted heavily, curve caps high marks.
        /// </summary>
        private float ComputeSweepPerformanceScore01(float coverageWidthMeters, float qualityStreakSeconds)
        {
            if (_validHitsTotal <= 0)
                return 0f;

            if (_settings.BaseZoneHitsOnly && _baseHitsTotal <= 0)
                return 0.05f;

            int sweepDenom = _settings.BaseZoneHitsOnly
                ? Mathf.Max(1, _baseHitsTotal)
                : Mathf.Max(1, _validHitsTotal);
            float qualityHitShare = Mathf.Clamp01((float)_hitsDuringQualityWindow / sweepDenom);

            float raw;
            if (_sweepRulePassed)
            {
                float needDur = Mathf.Max(0.05f, _settings.MinimumSweepDurationSeconds);
                float durT = Mathf.Clamp01(qualityStreakSeconds / (needDur * 2.6f));
                float blended = 0.62f * qualityHitShare + 0.38f * durT;
                raw = 0.1f + 0.78f * blended;
            }
            else if (_sweepPerformed)
            {
                float minSpan = Mathf.Max(0.01f, _settings.MinimumHorizontalSpanMeters);
                float spanT = Mathf.Clamp01((coverageWidthMeters - minSpan) / (minSpan * 2f));
                float needDur = Mathf.Max(0.05f, _settings.MinimumSweepDurationSeconds);
                float durT = Mathf.Clamp01(qualityStreakSeconds / (needDur * 1.15f));
                float basePartial = Mathf.Clamp01(0.28f + 0.28f * spanT + 0.32f * durT);
                raw = basePartial * (0.22f + 0.78f * qualityHitShare);
            }
            else if (_baseHitsTotal > 0)
                raw = 0.12f;
            else
                raw = 0.06f;

            raw = Mathf.Clamp01(raw);
            float curved = Mathf.Pow(raw, 1.12f);
            return Mathf.Clamp01(curved * 0.94f);
        }

        private string BuildFeedback()
        {
            if (_validHitsTotal == 0)
                return "No spray hits recorded.";

            if (_settings.BaseZoneHitsOnly && _baseHitsTotal == 0)
                return "Fire base was not targeted.";

            if (!_sweepPerformed)
                return "Spray was held on a single point.";

            if (_sweepRulePassed)
                return "Fire base was swept horizontally.";

            if (_anyQualitySnapshot && _maxQualityStreakSeconds + 1e-4f < _settings.MinimumSweepDurationSeconds)
                return "Sweep motion was too short.";

            if (!_anyQualitySnapshot)
                return "Sweep was too abrupt or samples were not spread enough in time.";

            return "Sweep did not fully meet training criteria.";
        }

        /// <summary>Live values for debug HUD (uses <see cref="Time.time"/> for window cutoff).</summary>
        public void GetRuntimeDebug(
            out int samplesInWindow,
            out float windowSpanMeters,
            out bool sweepPerformedSession,
            out bool performedLiveWindow,
            out bool sweepRulePassed,
            out float peakSpanMeters,
            out float bestQualityStreakSeconds,
            out int validHitsTotal,
            out int baseHitsTotal)
        {
            float now = Time.time;
            float cutoff = now - _settings.RollingWindowSeconds;
            _scratch.Clear();
            float tMin = float.MaxValue;
            float tMax = float.MinValue;
            for (int i = 0; i < _samples.Count; i++)
            {
                Sample s = _samples[i];
                if (s.Time < cutoff)
                    continue;
                if (s.Time < tMin) tMin = s.Time;
                if (s.Time > tMax) tMax = s.Time;
                _scratch.Add(new Vector3(s.X, 0f, s.Z));
            }

            samplesInWindow = _scratch.Count;
            windowSpanMeters = _scratch.Count >= _settings.MinimumSampleCount
                ? ComputeMaxPairwiseDistanceXZ(_scratch)
                : 0f;
            float temporalSpreadLive = samplesInWindow > 0 && tMax >= tMin ? tMax - tMin : 0f;
            bool performedTemporalLive = _settings.MinimumPerformedTemporalSpreadSeconds <= 1e-6f
                || temporalSpreadLive >= _settings.MinimumPerformedTemporalSpreadSeconds;
            performedLiveWindow = samplesInWindow >= _settings.MinimumSampleCount
                && windowSpanMeters >= _settings.MinimumHorizontalSpanMeters
                && performedTemporalLive;

            sweepPerformedSession = _sweepPerformed;
            sweepRulePassed = _sweepRulePassed;
            peakSpanMeters = _peakSpanQuality > 0.0001f ? _peakSpanQuality : _peakSpanPerformed;
            bestQualityStreakSeconds = _maxQualityStreakSeconds;
            if (_qualityStreakActive && _lastQualityTime >= _qualityStreakStartTime)
            {
                float open = _lastQualityTime - _qualityStreakStartTime;
                if (open > bestQualityStreakSeconds)
                    bestQualityStreakSeconds = open;
            }

            validHitsTotal = _validHitsTotal;
            baseHitsTotal = _baseHitsTotal;
        }

        public SpraySweepSettings CurrentSettings => _settings;

        private static float ComputeMaxPairwiseDistanceXZ(List<Sample> samples, List<Vector3> scratch)
        {
            scratch.Clear();
            for (int i = 0; i < samples.Count; i++)
                scratch.Add(new Vector3(samples[i].X, 0f, samples[i].Z));
            return ComputeMaxPairwiseDistanceXZ(scratch);
        }

        private static float ComputeMaxPairwiseDistanceXZ(List<Vector3> pts)
        {
            int n = pts.Count;
            if (n < 2)
                return 0f;

            float maxSq = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[i];
                for (int j = i + 1; j < n; j++)
                {
                    Vector3 b = pts[j];
                    float dx = a.x - b.x;
                    float dz = a.z - b.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 > maxSq)
                        maxSq = d2;
                }
            }

            return Mathf.Sqrt(maxSq);
        }
    }
}
