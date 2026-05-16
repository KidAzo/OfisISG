using FireExtinguisher.Core;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.Equipment
{
    /// <summary>
    /// Game-layer UI presenter for the equipped extinguisher info panel.
    /// Reacts to <see cref="PlayerExtinguisherEquipment"/> C# events to show or hide
    /// the panel, reads static data from <see cref="ExtinguisherData"/> on equip,
    /// and updates live capacity from the SO capacity event.
    /// </summary>
    /// <remarks>
    /// This component contains no spray or capacity logic — it only reads and
    /// displays data produced by the core framework.
    ///
    /// Expected UI element IDs in the UXML:
    ///   "equipment-panel"      — root container (shown/hidden)
    ///   "equipment-name"       — Label: display name
    ///   "equipment-type"       — Label: extinguisher agent type
    ///   "equipment-capacity"   — Label: remaining capacity as integer 0-100 %
    ///   "equipment-max"        — Label: max capacity in kg / units
    /// </remarks>
    [AddComponentMenu("Woi/Equipment/Extinguisher UI Presenter")]
    public sealed class ExtinguisherUIPresenter : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Data Source")]
        [Tooltip("The player equipment component to observe.")]
        [SerializeField] private PlayerExtinguisherEquipment _equipment;

        [Header("SO Event Bridge")]
        [Tooltip("Same ScriptableEventInt wired into ExtinguisherController. " +
                 "Payload: remaining capacity as integer 0–100.")]
        [SerializeField] private ScriptableEventInt _capacityEvent;

        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        // ── UI element refs ───────────────────────────────────────────────────────

        private VisualElement _panel;
        private Label         _nameLabel;
        private Label         _typeLabel;
        private Label         _capacityLabel;
        private Label         _maxLabel;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private bool _hasEquippedItem;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindUIElements();

            if (_equipment != null)
            {
                _equipment.OnExtinguisherChanged += HandleExtinguisherChanged;
            }
            else
            {
                Debug.LogWarning("[ExtinguisherUIPresenter] No PlayerExtinguisherEquipment assigned.", this);
            }

            if (_capacityEvent != null)
                _capacityEvent.OnRaised += HandleCapacityChanged;
            else
                Debug.LogWarning("[ExtinguisherUIPresenter] No capacity ScriptableEventInt assigned.", this);

            // Start hidden — nothing is equipped yet.
            SetPanelVisible(false);
        }

        private void OnDisable()
        {
            if (_equipment != null)
            {
                _equipment.OnExtinguisherChanged -= HandleExtinguisherChanged;
            }

            if (_capacityEvent != null)
                _capacityEvent.OnRaised -= HandleCapacityChanged;
        }

        // ── Equipment event handler ───────────────────────────────────────────────

        private void HandleExtinguisherChanged(ExtinguisherPickupItem item)
        {
            if (item != null)
            {
                _hasEquippedItem = true;
                RefreshStaticInfo(item);
                SetPanelVisible(true);
            }
            else
            {
                _hasEquippedItem = false;
                ClearLabels();
                SetPanelVisible(false);
            }
        }

        // ── Capacity event handler ────────────────────────────────────────────────

        private void HandleCapacityChanged(int capacity)
        {
            if (!_hasEquippedItem) return;

            if (_capacityLabel != null)
                _capacityLabel.text = $"{capacity}%";
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private void BindUIElements()
        {
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            _panel         = root.Q<VisualElement>("equipment-panel");
            _nameLabel     = root.Q<Label>("equipment-name");
            _typeLabel     = root.Q<Label>("equipment-type");
            _capacityLabel = root.Q<Label>("equipment-capacity");
            _maxLabel      = root.Q<Label>("equipment-max");
        }

        private void RefreshStaticInfo(ExtinguisherPickupItem item)
        {
            ExtinguisherData data = item.Controller != null
                ? item.Controller.ExtinguisherData
                : null;

            if (_nameLabel != null)
                _nameLabel.text = item.DisplayName;

            if (_typeLabel != null)
                _typeLabel.text = data != null
                    ? FormatType(data.ExtinguisherType)
                    : "—";

            if (_maxLabel != null)
                _maxLabel.text = item.Controller != null
                    ? $"{item.Controller.MaxCapacity:F1} kg"
                    : "—";

            // Seed capacity label with the current charge on equip.
            if (_capacityLabel != null && item.Controller != null)
                _capacityLabel.text = $"{Mathf.RoundToInt(item.Controller.NormalizedCapacity * 100f)}%";
        }

        private void ClearLabels()
        {
            if (_nameLabel     != null) _nameLabel.text     = string.Empty;
            if (_typeLabel     != null) _typeLabel.text     = string.Empty;
            if (_capacityLabel != null) _capacityLabel.text = string.Empty;
            if (_maxLabel      != null) _maxLabel.text      = string.Empty;
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel == null) return;
            _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string FormatType(ExtinguisherType type) => type switch
        {
            ExtinguisherType.Water       => "Water",
            ExtinguisherType.Foam        => "Foam (AFFF)",
            ExtinguisherType.DryPowder   => "Dry Powder (ABC)",
            ExtinguisherType.CO2         => "CO₂",
            ExtinguisherType.WetChemical => "Wet Chemical",
            _                            => type.ToString(),
        };
    }
}
