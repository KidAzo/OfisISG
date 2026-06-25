using UnityEngine;
using Woi.Equipment;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Keeps <c>EstinguisherHUD</c> hidden until the player equips an extinguisher in the Server Room scenario.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Woi/Office Fire/Server Extinguisher HUD Bridge")]
    public sealed class OfficeFireServerExtinguisherHudBridge : MonoBehaviour
    {
        private const string DefaultHudObjectName = "EstinguisherHUD";

        [SerializeField]
        private GameObject extinguisherHud;

        [SerializeField]
        private PlayerExtinguisherEquipment extinguisherEquipment;

        [SerializeField]
        private bool hideHudWhenDropped = true;

        private void Awake()
        {
            ResolveHudReference();
            EnsureHudGameObjectActive();
            SetHudVisible(false);
        }

        private void OnEnable()
        {
            BindEquipmentListeners();
        }

        private void Start()
        {
            BindEquipmentListeners();
        }

        private void OnDisable()
        {
            UnbindEquipmentListeners();
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
