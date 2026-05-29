using Obvious.Soap;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Archive alarm interactable: hover outline + 3D instruction popup + E to dispatch <c>press_alarm</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Alarm")]
    public sealed class Alarm : MonoBehaviour, ISelectable, IHoverable
    {
        [SerializeField]
        private bool isSelectable = true;

        [SerializeField]
        private bool requireHoverToPress = true;

        [SerializeField]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private string actionId = "press_alarm";

        [SerializeField]
        private ScriptableEventNoParam alarmPressed;

        [Header("Instruction Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string instructionText = "Press E to activate the alarm";

        [SerializeField]
        [TextArea(1, 3)]
        private string instructionTextTurkish = "Alarmı çalıştırmak için E'ye basın";

        [Header("Instruction Placement")]
        [SerializeField]
        private Transform instructionAnchor;

        [Tooltip("Local offset from Instruction Anchor (or this transform when anchor is empty).")]
        [SerializeField]
        private Vector3 instructionLocalOffset = new Vector3(0f, 0.35f, 0f);

        [Tooltip("Popup size multiplier (1 = InteractHoverPopupHost default scale).")]
        [SerializeField]
        [Min(0.01f)]
        private float instructionPopupScale = 1f;

        [Header("Hover Outline")]
        [SerializeField]
        private Outline outline;

        [SerializeField]
        private bool useOutlineWidth;

        [SerializeField]
        [Min(0f)]
        private float hoverOutlineWidth = 5f;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        private bool _isHovered;
        private bool _loggedMissingOutline;
        private bool _alarmTriggered;
        private InstructionPromptController _instructionPrompt;
        private float _defaultOutlineWidth;

        public bool IsSelectable => isSelectable && (!_alarmTriggered) && (!requireHoverToPress || _isHovered);

        private void Awake()
        {
            RemoveLegacySelectableAction();
            EnsureInstructionPrompt();
        }

        private void Start()
        {
            EnsureInstructionPrompt();
            EnsureOutline();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Alarm] Ready on '{name}'. requireHoverToPress={requireHoverToPress}, outline={(outline != null ? outline.name : "none")}.",
                    this);
            }
        }

        private void OnDisable()
        {
            _instructionPrompt?.Hide();
        }

        private void LateUpdate()
        {
            _instructionPrompt?.Tick();
        }

        public void Hover(bool isHovered)
        {
            if (_isHovered == isHovered)
            {
                return;
            }

            _isHovered = isHovered;

            if (UsesExternalInstructionPrompt())
            {
                ApplyOutlineHover(isHovered);
            }
            else
            {
                EnsureInstructionPrompt();
                _instructionPrompt?.SetHovered(isHovered);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[Alarm] Hover {(isHovered ? "ENTER" : "EXIT")} on '{name}'.", this);
            }
        }

        public void Select(SelectionContext context)
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Alarm] Select called on '{name}'. isSelectable={isSelectable}, isHovered={_isHovered}, requireHoverToPress={requireHoverToPress}, IsSelectable={IsSelectable}.",
                    this);
            }

            if (!IsSelectable)
            {
                if (requireHoverToPress && !_isHovered)
                {
                    Debug.LogWarning(
                        $"[Alarm] E ignored on '{name}' — hover required but isHovered=false. Is PCHoverInteractor in scene?",
                        this);
                }
                else if (_alarmTriggered)
                {
                    Debug.Log($"[Alarm] E ignored on '{name}' — alarm already pressed.", this);
                }

                return;
            }

            PressAlarm();
        }

        public void PressAlarm()
        {
            if (_alarmTriggered)
            {
                return;
            }

            _alarmTriggered = true;
            isSelectable = false;
            _instructionPrompt?.Hide();

            if (enableDebugLogs)
            {
                Debug.Log($"[Alarm] PressAlarm on '{name}'.", this);
            }

            if (alarmPressed != null)
            {
                alarmPressed.Raise();
                if (enableDebugLogs)
                {
                    Debug.Log($"[Alarm] SOAP event raised: '{alarmPressed.name}'.", this);
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"[Alarm] alarmPressed SOAP event is not assigned on '{name}'.", this);
            }

            if (!TryResolveScenario(out OfficeFireScenarioController scenario))
            {
                Debug.LogWarning($"[Alarm] No active scenario found for '{actionId}' on '{name}'.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning($"[Alarm] actionId is empty on '{name}'.", this);
                return;
            }

            scenario.HandleAction(actionId);

            if (enableDebugLogs)
            {
                Debug.Log($"[Alarm] HandleAction('{actionId}') sent to '{scenario.name}'.", this);
            }
        }

        private bool TryResolveScenario(out OfficeFireScenarioController scenario)
        {
            if (targetScenario != null)
            {
                scenario = targetScenario;
                return true;
            }

            return OfficeFireActiveScenarioLocator.TryGetActive(out scenario);
        }

        private void EnsureInstructionPrompt()
        {
            if (UsesExternalInstructionPrompt())
            {
                return;
            }

            if (_instructionPrompt == null)
            {
                EnsureOutline();

                _instructionPrompt = new InstructionPromptController(
                    this,
                    resolveAnchor: () => instructionAnchor != null ? instructionAnchor : transform,
                    resolveLocalOffset: () => instructionLocalOffset,
                    resolveWorldScale: () => instructionPopupScale,
                    hideWhenNotSelectable: true,
                    hideWhenInstructionEmpty: true,
                    preferTurkish: true,
                    outline: outline,
                    useOutlineWidth: useOutlineWidth,
                    hoverOutlineWidth: hoverOutlineWidth);
            }

            _instructionPrompt.SetInstruction(
                string.IsNullOrWhiteSpace(instructionText) ? "Press E to activate the alarm" : instructionText,
                string.IsNullOrWhiteSpace(instructionTextTurkish)
                    ? "Alarmı çalıştırmak için E'ye basın"
                    : instructionTextTurkish);
        }

        private void EnsureOutline()
        {
            if (outline != null)
            {
                return;
            }

            outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = GetComponentInChildren<Outline>(true);
            }

            if (outline == null && !_loggedMissingOutline)
            {
                Debug.LogWarning(
                    $"[Alarm] Quick Outline not found on '{name}' or children.",
                    this);
                _loggedMissingOutline = true;
                return;
            }

            if (outline != null && _defaultOutlineWidth <= 0f)
            {
                _defaultOutlineWidth = outline.OutlineWidth;
            }
        }

        private bool UsesExternalInstructionPrompt()
        {
            return SelectableScenarioAction.UsesExternalInstructionPrompt(this);
        }

        private void ApplyOutlineHover(bool isHovered)
        {
            EnsureOutline();
            if (outline == null)
            {
                return;
            }

            if (_defaultOutlineWidth <= 0f)
            {
                _defaultOutlineWidth = outline.OutlineWidth;
            }

            if (useOutlineWidth)
            {
                outline.enabled = isHovered;
                outline.OutlineWidth = isHovered ? hoverOutlineWidth : _defaultOutlineWidth;
                return;
            }

            outline.enabled = isHovered;
        }

        private void RemoveLegacySelectableAction()
        {
            SelectableScenarioAction legacyAction = GetComponent<SelectableScenarioAction>();
            if (legacyAction == null)
            {
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Alarm] Removing legacy SelectableScenarioAction on '{name}' — alarm uses hover + E only.",
                    this);
            }

            Destroy(legacyAction);
        }
    }
}
