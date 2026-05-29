using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Standalone hover prompt for objects that do not already implement <see cref="IHoverable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Selectable Instruction Prompt")]
    public sealed class SelectableInstructionPrompt : MonoBehaviour, IHoverable
    {
        [Header("Instruction")]
        [SerializeField]
        [TextArea(1, 3)]
        private string instructionText = "Press E to interact";

        [SerializeField]
        [TextArea(1, 3)]
        private string instructionTextTurkish = "Etkileşim için E'ye basın";

        [SerializeField]
        private bool preferTurkish = true;

        [Header("Placement")]
        [SerializeField]
        private Transform anchor;

        [Tooltip("Local offset from Anchor (or this transform when anchor is empty).")]
        [SerializeField]
        private Vector3 localOffset = new Vector3(0f, 1.1f, 0f);

        [Header("Visibility")]
        [SerializeField]
        private bool hideWhenNotSelectable = true;

        [SerializeField]
        private bool hideWhenInstructionEmpty = true;

        [Header("Billboard")]
        [SerializeField]
        private Camera billboardCamera;

        [SerializeField]
        private bool autoResolvePlayerCamera = true;

        [Header("Hover Outline (Optional)")]
        [SerializeField]
        private Outline outline;

        [SerializeField]
        private bool useOutlineWidth;

        [SerializeField]
        [Min(0f)]
        private float hoverOutlineWidth = 5f;

        private InstructionPromptController _controller;

        public string InstructionText
        {
            get => instructionText;
            set => instructionText = value;
        }

        public string InstructionTextTurkish
        {
            get => instructionTextTurkish;
            set => instructionTextTurkish = value;
        }

        private void Awake()
        {
            EnsureController();
            SyncInstruction();
        }

        private void OnDisable()
        {
            _controller?.Hide();
        }

        private void LateUpdate()
        {
            _controller?.Tick();
        }

        public void Hover(bool isHovered)
        {
            EnsureController();
            SyncInstruction();
            _controller?.SetHovered(isHovered);
        }

        public void SetInstruction(string english, string turkish = null)
        {
            instructionText = english ?? string.Empty;
            if (turkish != null)
            {
                instructionTextTurkish = turkish;
            }

            EnsureController();
            _controller?.SetInstruction(instructionText, instructionTextTurkish);
        }

        private void EnsureController()
        {
            if (_controller != null)
            {
                return;
            }

            if (outline == null)
            {
                outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>(true);
            }

            _controller = new InstructionPromptController(
                this,
                resolveAnchor: () => anchor != null ? anchor : transform,
                resolveLocalOffset: () => localOffset,
                hideWhenNotSelectable,
                hideWhenInstructionEmpty,
                preferTurkish,
                outline,
                useOutlineWidth,
                hoverOutlineWidth);
        }

        private void SyncInstruction()
        {
            _controller?.SetInstruction(instructionText, instructionTextTurkish);
        }
    }
}
