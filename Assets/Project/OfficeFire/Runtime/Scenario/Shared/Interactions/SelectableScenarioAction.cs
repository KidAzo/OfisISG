using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public sealed class SelectableScenarioAction : MonoBehaviour, ISelectable, IHoverable
    {
        [SerializeField]
        private bool isSelectable = true;

        [SerializeField]
        private OfficeFireScenarioController targetScenario;

        [SerializeField]
        private string actionId;

        [Header("Instruction Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string instructionText;

        [SerializeField]
        [TextArea(1, 3)]
        private string instructionTextTurkish;

        [Header("Instruction Placement")]
        [SerializeField]
        private Transform instructionAnchor;

        [Tooltip("Local offset from Instruction Anchor (or this transform when anchor is empty).")]
        [SerializeField]
        private Vector3 instructionLocalOffset = new Vector3(0f, 1.1f, 0f);

        [Tooltip("Popup size multiplier (1 = InteractHoverPopupHost default scale).")]
        [SerializeField]
        [Min(0.01f)]
        private float instructionPopupScale = 1f;

        [SerializeField]
        private UnityEvent onSelected = new UnityEvent();

        private InstructionPromptController _instructionPrompt;

        public string ActionId => actionId;

        public bool IsSelectable => isSelectable;

        private void Awake()
        {
            EnsureInstructionPrompt();
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
            if (UsesExternalInstructionPrompt())
            {
                return;
            }

            EnsureInstructionPrompt();
            _instructionPrompt?.SetHovered(isHovered);
        }

        public void Select(SelectionContext context)
        {
            if (!isSelectable)
            {
                return;
            }

            if (targetScenario == null)
            {
                Debug.LogWarning("[SelectableScenarioAction] targetScenario is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[SelectableScenarioAction] actionId is empty.", this);
                return;
            }

            DispatchScenarioAction();
        }

        /// <summary>
        /// Invokes <see cref="OfficeFireScenarioController.HandleAction"/> (e.g. from <see cref="SelectableDoor"/> onOpened).
        /// </summary>
        public void DispatchScenarioAction()
        {
            if (targetScenario == null)
            {
                Debug.LogWarning("[SelectableScenarioAction] targetScenario is not assigned.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[SelectableScenarioAction] actionId is empty.", this);
                return;
            }

            targetScenario.HandleAction(actionId);

            if (actionId == "use_extinguisher" || actionId == "grab_extinguisher")
            {
                Debug.Log(
                    $"[SelectableScenarioAction] '{actionId}' -> '{targetScenario.name}' ({targetScenario.GetType().Name})",
                    this);
            }

            if (onSelected != null)
            {
                onSelected.Invoke();
            }
        }

        private void EnsureInstructionPrompt()
        {
            if (UsesExternalInstructionPrompt())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(instructionText) && string.IsNullOrWhiteSpace(instructionTextTurkish))
            {
                return;
            }

            if (_instructionPrompt == null)
            {
                _instructionPrompt = new InstructionPromptController(
                    this,
                    resolveAnchor: () => instructionAnchor != null ? instructionAnchor : transform,
                    resolveLocalOffset: () => instructionLocalOffset,
                    resolveWorldScale: () => instructionPopupScale,
                    hideWhenNotSelectable: true,
                    hideWhenInstructionEmpty: true,
                    preferTurkish: true,
                    outline: null,
                    useOutlineWidth: false,
                    hoverOutlineWidth: 5f);
            }

            _instructionPrompt.SetInstruction(instructionText, instructionTextTurkish);
        }

        public static bool UsesExternalInstructionPrompt(MonoBehaviour host)
        {
            if (host == null)
            {
                return false;
            }

            return host.GetComponent<SelectableInstructionPrompt>() != null
                || host.GetComponentInChildren<SelectableInstructionPrompt>(true) != null;
        }

        private bool UsesExternalInstructionPrompt()
        {
            return UsesExternalInstructionPrompt(this);
        }
    }
}
