using System.Collections.Generic;
using System.Reflection;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Dispatches <c>use_extinguisher</c> when server room fire suppression actually begins,
    /// and notifies the scenario when the fire is fully out.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Woi/Office Fire/Server Fire Extinguish Bridge")]
    public sealed class OfficeFireServerFireExtinguishBridge : MonoBehaviour
    {
        private const float IntensityDecreaseEpsilon = 0.0001f;

        [SerializeField]
        private ServerRoomScenarioController scenario;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private PlayerExtinguisherEquipment extinguisherEquipment;

        [SerializeField]
        private PlayerExtinguisherEquipment xrExtinguisherEquipment;

        [SerializeField]
        private bool dispatchUseExtinguisherOnExtinguishingStarted = true;

        [SerializeField]
        private bool notifyScenarioOnFullyExtinguished = true;

        [Header("Layer fix")]
        [Tooltip("Server fire zones use layer 9, but extinguisher prefabs often mask layer 6 only. Merge this mask at runtime.")]
        [SerializeField]
        private bool autoFixEvaluatorFireZoneLayerMask = true;

        [SerializeField]
        private LayerMask serverFireZoneLayerMask = 1 << 9;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        [SerializeField]
        [Min(0.25f)]
        private float progressLogIntervalSeconds = 1f;

        private float _nextProgressLogTime;
        private float _lastLoggedProgress = -1f;
        private float _baselineIntensity = -1f;
        private float _peakIntensity = -1f;
        private ExtinguisherController _subscribedController;
        private bool _useExtinguisherDispatched;
        private readonly List<PlayerExtinguisherEquipment> _boundEquipments = new(2);
        private static FieldInfo s_evaluatorLayerMaskField;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = GetComponent<ServerRoomScenarioController>();
            }
        }

        private void Start()
        {
            RemoveSuppressionGates();

            if (autoFixEvaluatorFireZoneLayerMask)
            {
                TryFixEvaluatorLayerMasks();
            }

            BindEquipmentListeners();
            SnapshotIntensityBaseline();
            LogProgress(force: true);
        }

        public void AllowExtinguisherSpray()
        {
            RemoveSuppressionGates();
            _useExtinguisherDispatched = false;
            SnapshotIntensityBaseline();
            Log("Sondurme izleme sifirlandi — yangin her zaman sondurulebilir.");
            LogProgress(force: true);
        }

        private void RemoveSuppressionGates()
        {
            ResolveFireSource();
            if (fireSource == null)
            {
                return;
            }

            FireExtinguishPrerequisiteGate[] gates =
                fireSource.GetComponents<FireExtinguishPrerequisiteGate>();
            for (int i = 0; i < gates.Length; i++)
            {
                FireExtinguishPrerequisiteGate gate = gates[i];
                if (gate != null)
                {
                    Destroy(gate);
                }
            }

            Log("On kosul kaldirildi — sprey bloklanmiyor.");
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
            BindEquipmentListeners();

            if (fireSource == null)
            {
                LogWarning("FireSource atanmadi — sondurme olaylari senaryoya bildirilmeyecek.");
                return;
            }

            SnapshotIntensityBaseline();
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
            UnbindEquipmentListeners();
            UnsubscribeController(_subscribedController);
            _useExtinguisherDispatched = false;
            _baselineIntensity = -1f;
            _peakIntensity = -1f;

            if (fireSource == null)
            {
                return;
            }

            fireSource.OnFullyExtinguished -= HandleFullyExtinguished;
            fireSource.OnIntensityChanged -= HandleIntensityChanged;
            fireSource.OnStateChanged -= HandleStateChanged;
        }

        private void BindEquipmentListeners()
        {
            UnbindEquipmentListeners();

            TryBindEquipment(extinguisherEquipment);
            TryBindEquipment(xrExtinguisherEquipment);

            if (_boundEquipments.Count == 0)
            {
                PlayerExtinguisherEquipment[] found = FindObjectsByType<PlayerExtinguisherEquipment>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < found.Length; i++)
                {
                    TryBindEquipment(found[i]);
                }
            }

            RebindSprayController();
        }

        private void TryBindEquipment(PlayerExtinguisherEquipment equipment)
        {
            if (equipment == null || _boundEquipments.Contains(equipment))
            {
                return;
            }

            equipment.OnExtinguisherChanged += HandleEquipmentChanged;
            _boundEquipments.Add(equipment);
        }

        private void UnbindEquipmentListeners()
        {
            for (int i = 0; i < _boundEquipments.Count; i++)
            {
                PlayerExtinguisherEquipment equipment = _boundEquipments[i];
                if (equipment != null)
                {
                    equipment.OnExtinguisherChanged -= HandleEquipmentChanged;
                }
            }

            _boundEquipments.Clear();
        }

        private void HandleEquipmentChanged(ExtinguisherPickupItem item)
        {
            RebindSprayController();
        }

        private void RebindSprayController()
        {
            ExtinguisherPickupItem equippedItem = ResolveEquippedItem();
            SubscribeController(equippedItem != null ? equippedItem.Controller : null);
        }

        private ExtinguisherPickupItem ResolveEquippedItem()
        {
            for (int i = 0; i < _boundEquipments.Count; i++)
            {
                PlayerExtinguisherEquipment equipment = _boundEquipments[i];
                if (equipment?.CurrentItem != null)
                {
                    return equipment.CurrentItem;
                }
            }

            return null;
        }

        private void SubscribeController(ExtinguisherController next)
        {
            if (next == _subscribedController)
            {
                return;
            }

            UnsubscribeController(_subscribedController);
            _subscribedController = next;

            if (_subscribedController == null)
            {
                return;
            }

            _subscribedController.OnSprayEvaluated += HandleSprayEvaluated;
        }

        private void UnsubscribeController(ExtinguisherController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.OnSprayEvaluated -= HandleSprayEvaluated;

            if (_subscribedController == controller)
            {
                _subscribedController = null;
            }
        }

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            if (!ShouldDispatchUseExtinguisher())
            {
                return;
            }

            ResolveFireSource();
            if (fireSource == null || result.Source != fireSource)
            {
                return;
            }

            if (!result.DidHitZone || result.ExtinguishAmountCalculated <= 0f)
            {
                return;
            }

            if (result.Compatibility != CompatibilityResult.Effective)
            {
                return;
            }

            Log(
                $"Effective spray hit server fire — amount={result.ExtinguishAmountCalculated:F5}, " +
                $"intensity={fireSource.CurrentNormalizedIntensity:F3}");
            DispatchUseExtinguisher();
        }

        private void SnapshotIntensityBaseline()
        {
            if (fireSource == null)
            {
                _baselineIntensity = -1f;
                _peakIntensity = -1f;
                return;
            }

            _baselineIntensity = fireSource.CurrentNormalizedIntensity;
            _peakIntensity = _baselineIntensity;
        }

        private void Update()
        {
            TryDispatchFromIntensityPolling();

            if (!enableDebugLogs || fireSource == null)
            {
                return;
            }

            if (Time.time >= _nextProgressLogTime)
            {
                LogProgress(force: false);
            }
        }

        private void TryDispatchFromIntensityPolling()
        {
            if (!ShouldDispatchUseExtinguisher() || fireSource == null || _baselineIntensity < 0f)
            {
                return;
            }

            float current = fireSource.CurrentNormalizedIntensity;
            if (current > _peakIntensity)
            {
                _peakIntensity = current;
            }

            if (current < _peakIntensity - IntensityDecreaseEpsilon)
            {
                Log(
                    $"Server fire intensity dropped (poll) — peak={_peakIntensity:F4}, current={current:F4}");
                DispatchUseExtinguisher();
            }
        }

        private bool ShouldDispatchUseExtinguisher()
        {
            return dispatchUseExtinguisherOnExtinguishingStarted && !_useExtinguisherDispatched;
        }

        private void HandleStateChanged(FireSourceState state)
        {
            Log($"FireSource state -> {state} (intensity={fireSource.CurrentNormalizedIntensity:F2})");
            LogProgress(force: true);
        }

        private void HandleIntensityChanged(float normalizedIntensity)
        {
            if (normalizedIntensity > _peakIntensity)
            {
                _peakIntensity = normalizedIntensity;
            }

            if (ShouldDispatchUseExtinguisher()
                && _peakIntensity >= 0f
                && normalizedIntensity < _peakIntensity - IntensityDecreaseEpsilon)
            {
                Log(
                    $"Server fire intensity dropped (event) — peak={_peakIntensity:F4}, current={normalizedIntensity:F4}");
                DispatchUseExtinguisher();
            }

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

        private void DispatchUseExtinguisher()
        {
            if (scenario == null)
            {
                LogWarning("ServerRoomScenarioController yok — use_extinguisher gonderilemedi.");
                return;
            }

            _useExtinguisherDispatched = true;
            scenario.LogFireExtinguishStatus("Yangin sondurulmeye basladi — use_extinguisher gonderiliyor");
            Log("Dispatching use_extinguisher.");
            scenario.HandleAction(ServerRoomScenarioController.Actions.UseExtinguisher);
        }

        private void HandleFullyExtinguished()
        {
            LogProgress(force: true);
            Log("FireSource tamamen sonduruldu (OnFullyExtinguished).");

            if (scenario == null)
            {
                LogWarning("ServerRoomScenarioController yok — tam sondurme bildirilemedi.");
                return;
            }

            if (!notifyScenarioOnFullyExtinguished)
            {
                Log("notifyScenarioOnFullyExtinguished=false — tam sondurme bildirilmiyor.");
                return;
            }

            if (!scenario.CanExtinguishFire(out string reason))
            {
                LogWarning($"Yangin sonduruldu ama senaryo kabul etmiyor — {reason}");
                return;
            }

            scenario.NotifyFireFullyExtinguished();
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

            Debug.Log(
                $"[ServerFireExtinguishBridge] {prefix} — sondurme: %{progressPercent:F0} | " +
                $"kalan intensity={normalizedIntensity:F2} | fireState={fireSource.State} | " +
                $"scenario={scenarioState} | {canReason}",
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

            int requiredBits = serverFireZoneLayerMask.value;
            if (requiredBits == 0)
            {
                LogWarning("serverFireZoneLayerMask=0 — layer duzeltmesi atlandi.");
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
                    continue;
                }

                int merged = current.value | requiredBits;
                s_evaluatorLayerMaskField.SetValue(evaluator, (LayerMask)merged);
                LogWarning(
                    $"ExtinguishEvaluator '{evaluator.name}' layerMask duzeltildi: {current.value} -> {merged}");
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ServerFireExtinguishBridge] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ServerFireExtinguishBridge] {message}", this);
        }
    }
}
