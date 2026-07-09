using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Shows a 3D instruction popup on a <see cref="FireTargetZone"/> when the player is carrying a blanket
    /// and is close enough to place it (same proximity check as <see cref="FireBlanketUseController"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Fire Blanket Fire Zone Use Prompt")]
    public sealed class FireBlanketFireZoneUsePrompt : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private FireTargetZone fireZone;

        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private PlayerFireBlanketEquipment blanketEquipment;

        [SerializeField]
        private FireBlanketUseController blanketUseController;

        [Header("Instruction Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string instructionText = "Press G to place the blanket";

        [SerializeField]
        [TextArea(1, 3)]
        private string instructionTextTurkish = "G ile bırak";

        [Header("Instruction Placement")]
        [SerializeField]
        private Transform instructionAnchor;

        [Tooltip("Local offset from Instruction Anchor (or this transform when anchor is empty).")]
        [SerializeField]
        private Vector3 instructionLocalOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("Popup size multiplier (1 = InteractHoverPopupHost default scale).")]
        [SerializeField]
        [Min(0.01f)]
        private float instructionPopupScale = 1f;

        [Header("Hover Outline (Optional)")]
        [SerializeField]
        private Outline outline;

        [SerializeField]
        private bool useOutlineWidth = true;

        [SerializeField]
        [Min(0f)]
        private float hoverOutlineWidth = 5f;

        private InstructionPromptController _instructionPrompt;

        private void Awake()
        {
            if (fireZone == null)
            {
                fireZone = GetComponent<FireTargetZone>();
            }

            if (fireSource == null && fireZone != null)
            {
                fireSource = fireZone.GetComponentInParent<FireSource>();
            }

            ResolveRuntimeReferences();

            if (blanketUseController != null)
            {
                enabled = false;
                return;
            }

            EnsureInstructionPrompt();
        }

        private void OnDisable()
        {
            _instructionPrompt?.Hide();
        }

        private void LateUpdate()
        {
            bool show = ShouldShowPrompt();
            _instructionPrompt?.SetHovered(show);
            _instructionPrompt?.Tick();
        }

        private bool ShouldShowPrompt()
        {
            ResolveRuntimeReferences();

            if (fireZone == null || blanketEquipment == null || blanketUseController == null)
            {
                return false;
            }

            if (blanketEquipment.CurrentItem == null)
            {
                return false;
            }

            if (blanketUseController.IsExtinguishingFire)
            {
                return false;
            }

            if (fireZone.IsExtinguished)
            {
                return false;
            }

            if (fireSource != null && fireSource.IsExtinguished)
            {
                return false;
            }

            // Single zone query (IsInsideFireZone + TryGetTargetFireZone previously ran CheckInsideFireZone twice).
            return blanketUseController.TryGetTargetFireZone(out FireTargetZone matchedZone)
                && (fireZone == null || matchedZone == fireZone);
        }

        private void ResolveRuntimeReferences()
        {
            if (blanketEquipment == null)
            {
                blanketEquipment = FindFirstObjectByType<PlayerFireBlanketEquipment>(FindObjectsInactive.Include);
            }

            if (blanketUseController == null && blanketEquipment != null)
            {
                blanketUseController = blanketEquipment.GetComponent<FireBlanketUseController>();
            }

            if (blanketUseController == null)
            {
                blanketUseController = FindFirstObjectByType<FireBlanketUseController>(FindObjectsInactive.Include);
            }

            if (blanketEquipment == null && blanketUseController != null)
            {
                blanketEquipment = blanketUseController.GetComponent<PlayerFireBlanketEquipment>();
            }
        }

        private void EnsureInstructionPrompt()
        {
            if (_instructionPrompt != null)
            {
                return;
            }

            if (outline == null)
            {
                outline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>(true);
            }

            _instructionPrompt = new InstructionPromptController(
                this,
                resolveAnchor: () => instructionAnchor != null ? instructionAnchor : transform,
                resolveLocalOffset: () => instructionLocalOffset,
                resolveWorldScale: () => instructionPopupScale,
                hideWhenNotSelectable: false,
                hideWhenInstructionEmpty: true,
                preferTurkish: true,
                outline: outline,
                useOutlineWidth: useOutlineWidth,
                hoverOutlineWidth: hoverOutlineWidth);

            _instructionPrompt.SetInstruction(instructionText, instructionTextTurkish);
        }
    }
}
