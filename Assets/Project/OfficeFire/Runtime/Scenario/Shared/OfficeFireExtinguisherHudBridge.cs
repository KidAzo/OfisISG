using System.Collections;
using UnityEngine;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Keeps <c>EstinguisherHUD</c> hidden until the player equips an extinguisher.
    /// Lives on an always-active object (e.g. <see cref="OfficeFireScenarioBootstrapper"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Woi/Office Fire/Extinguisher HUD Bridge")]
    public class OfficeFireExtinguisherHudBridge : MonoBehaviour
    {
        [SerializeField]
        private GameObject extinguisherHud;

        [SerializeField]
        private PlayerExtinguisherEquipment extinguisherEquipment;

        [SerializeField]
        private bool hideHudWhenDropped = true;

        private Coroutine _bindEquipmentRoutine;

        private void Awake()
        {
            ResolveHudReference();
            EnsureHudGameObjectActive();
            SetHudVisible(false);
        }

        private void OnEnable()
        {
            BindEquipmentListeners();
            StartBindEquipmentRetry();
        }

        private void Start()
        {
            BindEquipmentListeners();
            StartBindEquipmentRetry();
        }

        private void OnDisable()
        {
            StopBindEquipmentRetry();
            UnbindEquipmentListeners();
        }

        internal static void EnsureOnBootstrapper()
        {
            OfficeFireScenarioBootstrapper bootstrapper =
                FindAnyObjectByType<OfficeFireScenarioBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                return;
            }

            OfficeFireExtinguisherHudBridge bridge =
                bootstrapper.GetComponent<OfficeFireExtinguisherHudBridge>();
            if (bridge == null)
            {
                bridge = bootstrapper.gameObject.AddComponent<OfficeFireExtinguisherHudBridge>();
            }

            bridge.RefreshBinding();
        }

        public void RefreshBinding()
        {
            ResolveHudReference();
            EnsureHudGameObjectActive();
            BindEquipmentListeners();

            if (extinguisherEquipment != null && extinguisherEquipment.CurrentItem != null)
            {
                SetHudVisible(true);
            }
        }

        private void StartBindEquipmentRetry()
        {
            if (_bindEquipmentRoutine != null)
            {
                return;
            }

            _bindEquipmentRoutine = StartCoroutine(BindEquipmentWhenReady());
        }

        private void StopBindEquipmentRetry()
        {
            if (_bindEquipmentRoutine == null)
            {
                return;
            }

            StopCoroutine(_bindEquipmentRoutine);
            _bindEquipmentRoutine = null;
        }

        private IEnumerator BindEquipmentWhenReady()
        {
            const int maxFrames = 300;
            for (int i = 0; i < maxFrames && isActiveAndEnabled; i++)
            {
                BindEquipmentListeners();
                if (extinguisherEquipment != null)
                {
                    break;
                }

                yield return null;
            }

            _bindEquipmentRoutine = null;
        }

        private void BindEquipmentListeners()
        {
            if (extinguisherEquipment == null)
            {
                extinguisherEquipment = FindFirstObjectByType<PlayerExtinguisherEquipment>(
                    FindObjectsInactive.Include);
            }

            if (extinguisherEquipment == null)
            {
                return;
            }

            extinguisherEquipment.OnExtinguisherChanged -= HandleExtinguisherChanged;
            extinguisherEquipment.OnExtinguisherChanged += HandleExtinguisherChanged;

            if (extinguisherEquipment.CurrentItem != null)
            {
                SetHudVisible(true);
            }
        }

        private void UnbindEquipmentListeners()
        {
            if (extinguisherEquipment == null)
            {
                return;
            }

            extinguisherEquipment.OnExtinguisherChanged -= HandleExtinguisherChanged;
        }

        private void HandleExtinguisherChanged(ExtinguisherPickupItem item)
        {
            if (item != null)
            {
                SetHudVisible(true);
                return;
            }

            if (hideHudWhenDropped)
            {
                SetHudVisible(false);
            }
        }

        private void ResolveHudReference()
        {
            extinguisherHud = OfficeFireExtinguisherHudReference.Resolve(extinguisherHud);
        }

        private void EnsureHudGameObjectActive()
        {
            ResolveHudReference();
            if (extinguisherHud != null && !extinguisherHud.activeSelf)
            {
                extinguisherHud.SetActive(true);
            }
        }

        private void SetHudVisible(bool visible)
        {
            ResolveHudReference();
            if (extinguisherHud == null)
            {
                Debug.LogWarning("[OfficeFireExtinguisherHudBridge] EstinguisherHUD not found.", this);
                return;
            }

            if (!extinguisherHud.activeSelf)
            {
                extinguisherHud.SetActive(true);
            }

            extinguisherHud.SendMessage(
                "SetPresentationVisible",
                visible,
                SendMessageOptions.DontRequireReceiver);
        }
    }
}
