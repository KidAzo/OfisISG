using System.Reflection;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Listens to <see cref="FireSource"/> and dispatches <c>use_extinguisher</c> when the fire is fully extinguished.
    /// Blocks physical spray until the archive alarm is pressed.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Woi/Office Fire/Archive Fire Extinguish Bridge")]
    public sealed class OfficeFireArchiveFireExtinguishBridge : MonoBehaviour
    {
        [SerializeField]
        private ArchiveRoomScenarioController scenario;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private bool dispatchOnFullyExtinguished = true;

        [Header("Layer fix")]
        [Tooltip("Archive fire zones use layer 9, but extinguisher prefabs often mask layer 6 only. Merge this mask at runtime.")]
        [SerializeField]
        private bool autoFixEvaluatorFireZoneLayerMask = true;

        [SerializeField]
        private LayerMask archiveFireZoneLayerMask = 1 << 9;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        [SerializeField]
        [Min(0.25f)]
        private float progressLogIntervalSeconds = 1f;

        private float _nextProgressLogTime;
        private float _lastLoggedProgress = -1f;
        private FireExtinguishPrerequisiteGate _alarmGate;
        private static FieldInfo s_evaluatorLayerMaskField;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<ArchiveRoomScenarioController>();
            }
        }

        private void Start()
        {
            EnsureAlarmPrerequisiteGate();
            DisableLegacyElectricalSafety();

            if (autoFixEvaluatorFireZoneLayerMask)
            {
                TryFixEvaluatorLayerMasks();
            }

            LogProgress(force: true);
        }

        /// <summary>
        /// Alarm sonrasi sondurucu spreyinin yangina etki etmesini acar.
        /// </summary>
        public void AllowExtinguisherSpray()
        {
            EnsureAlarmPrerequisiteGate();
            if (_alarmGate != null)
            {
                _alarmGate.ForceAllowExtinguisher();
            }

            Log("Sprey kilidi acildi — alarm sonrasi sondurme aktif.");
            LogProgress(force: true);
        }

        private void EnsureAlarmPrerequisiteGate()
        {
            ResolveFireSource();
            if (fireSource == null)
            {
                return;
            }

            GameObject fireObject = fireSource.gameObject;
            FireExtinguishPrerequisiteGate[] gates = fireObject.GetComponents<FireExtinguishPrerequisiteGate>();
            for (int i = 0; i < gates.Length; i++)
            {
                FireExtinguishPrerequisiteGate gate = gates[i];
                if (gate == null)
                {
                    continue;
                }

                if (gate.Mode == FireExtinguishPrerequisiteGate.GateMode.ManualOnly)
                {
                    _alarmGate = gate;
                    continue;
                }

                Destroy(gate);
            }

            if (_alarmGate == null)
            {
                _alarmGate = fireObject.AddComponent<FireExtinguishPrerequisiteGate>();
            }

            _alarmGate.ConfigureForManualOnly();
            Log("Alarm on kosulu aktif — sprey alarm basilana kadar bloklu.");
        }

        private void DisableLegacyElectricalSafety()
        {
            OfficeFireArchiveElectricalSafetySetup legacySetup =
                FindFirstObjectByType<OfficeFireArchiveElectricalSafetySetup>(FindObjectsInactive.Include);
            if (legacySetup != null)
            {
                legacySetup.enabled = false;
            }
        }

        private void ResolveFireSource()
        {
            if (fireSource == null)
            {
                fireSource = FindFirstObjectByType<FireSource>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            ResolveFireSource();
            if (fireSource == null)
            {
                LogWarning("FireSource atanmadi — sprey ile sondurme senaryoya bildirilmeyecek.");
                return;
            }

            fireSource.OnFullyExtinguished += HandleFullyExtinguished;
            fireSource.OnIntensityChanged += HandleIntensityChanged;
            fireSource.OnStateChanged += HandleStateChanged;

            Log(
                $"FireSource '{fireSource.name}' dinleniyor. " +
                $"IsExtinguished={fireSource.IsExtinguished}, intensity={fireSource.CurrentNormalizedIntensity:F2}");
            scenario?.LogFireExtinguishStatus("FireExtinguishBridge aktif");
        }

        private void OnDisable()
        {
            if (fireSource == null)
            {
                return;
            }

            fireSource.OnFullyExtinguished -= HandleFullyExtinguished;
            fireSource.OnIntensityChanged -= HandleIntensityChanged;
            fireSource.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (!enableDebugLogs || fireSource == null)
            {
                return;
            }

            if (Time.time >= _nextProgressLogTime)
            {
                LogProgress(force: false);
            }
        }

        private void HandleStateChanged(FireSourceState state)
        {
            Log($"FireSource state -> {state} (intensity={fireSource.CurrentNormalizedIntensity:F2})");
            LogProgress(force: true);
        }

        private void HandleIntensityChanged(float normalizedIntensity)
        {
            float progress = GetExtinguishProgressPercent(normalizedIntensity);
            if (!enableDebugLogs)
            {
                return;
            }

            if (Mathf.Abs(progress - _lastLoggedProgress) < 1f)
            {
                return;
            }

            _lastLoggedProgress = progress;
            LogIntensitySnapshot(normalizedIntensity, progress, "Sprey etkisi");
        }

        private void HandleFullyExtinguished()
        {
            LogProgress(force: true);
            Log("FireSource tamamen sonduruldu (OnFullyExtinguished).");

            if (scenario == null)
            {
                LogWarning("ArchiveRoomScenarioController yok — use_extinguisher gonderilemedi.");
                return;
            }

            if (!scenario.CanExtinguishFire(out string reason))
            {
                LogWarning($"Yangin sonduruldu ama senaryo kabul etmiyor — {reason}");
                return;
            }

            if (!dispatchOnFullyExtinguished)
            {
                Log("dispatchOnFullyExtinguished=false — use_extinguisher gonderilmiyor.");
                return;
            }

            scenario.LogFireExtinguishStatus("FireSource sonduruldu — use_extinguisher gonderiliyor");
            scenario.HandleAction(ArchiveRoomScenarioController.Actions.UseExtinguisher);
        }

        private void LogProgress(bool force)
        {
            if (!enableDebugLogs || fireSource == null)
            {
                return;
            }

            _nextProgressLogTime = Time.time + progressLogIntervalSeconds;

            float remaining = fireSource.CurrentNormalizedIntensity;
            float progress = GetExtinguishProgressPercent(remaining);

            if (!force && Mathf.Abs(progress - _lastLoggedProgress) < 0.5f)
            {
                return;
            }

            _lastLoggedProgress = progress;
            LogIntensitySnapshot(remaining, progress, "Durum");
        }

        private void LogIntensitySnapshot(float normalizedIntensity, float progressPercent, string prefix)
        {
            string scenarioState = scenario != null ? scenario.CurrentState.ToString() : "yok";
            string canReason = "n/a";
            if (scenario != null)
            {
                scenario.CanExtinguishFire(out canReason);
            }

            string gateStatus = "yok";
            if (_alarmGate != null)
            {
                gateStatus = _alarmGate.CanExtinguish ? "acik" : "kapali (alarm gerekli)";
            }

            Debug.Log(
                $"[ArchiveFireExtinguishBridge] {prefix} — sondurme: %{progressPercent:F0} | " +
                $"kalan intensity={normalizedIntensity:F2} | fireState={fireSource.State} | " +
                $"scenario={scenarioState} | gate={gateStatus} | {canReason}",
                this);
        }

        private static float GetExtinguishProgressPercent(float normalizedIntensity)
        {
            return Mathf.Clamp01(1f - normalizedIntensity) * 100f;
        }

        private void TryFixEvaluatorLayerMasks()
        {
            s_evaluatorLayerMaskField ??= typeof(ExtinguishEvaluator).GetField(
                "_fireZoneLayerMask",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (s_evaluatorLayerMaskField == null)
            {
                LogWarning("ExtinguishEvaluator._fireZoneLayerMask bulunamadi — otomatik layer duzeltmesi atlandi.");
                return;
            }

            int requiredBits = archiveFireZoneLayerMask.value;
            if (requiredBits == 0)
            {
                LogWarning("archiveFireZoneLayerMask=0 — layer duzeltmesi atlandi.");
                return;
            }

            ExtinguishEvaluator[] evaluators = FindObjectsByType<ExtinguishEvaluator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (evaluators.Length == 0)
            {
                LogWarning("Sahnede ExtinguishEvaluator bulunamadi.");
                return;
            }

            foreach (ExtinguishEvaluator evaluator in evaluators)
            {
                if (evaluator == null)
                {
                    continue;
                }

                var current = (LayerMask)s_evaluatorLayerMaskField.GetValue(evaluator);
                if ((current.value & requiredBits) == requiredBits)
                {
                    Log($"ExtinguishEvaluator '{evaluator.name}' layerMask={current.value} (layer 9 zaten acik)");
                    continue;
                }

                int merged = current.value | requiredBits;
                s_evaluatorLayerMaskField.SetValue(evaluator, (LayerMask)merged);
                LogWarning(
                    $"ExtinguishEvaluator '{evaluator.name}' layerMask duzeltildi: {current.value} -> {merged} " +
                    $"(yangin zone layer 9 eklendi — once sprey zone'a isabet etmiyordu)");
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ArchiveFireExtinguishBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ArchiveFireExtinguishBridge] {message}", this);
        }
    }
}
