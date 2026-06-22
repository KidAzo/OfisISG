using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public sealed class SelectableDoor : MonoBehaviour, ISelectable, IHoverable
    {
        private static readonly SelectionContext DefaultSelectionContext =
            new SelectionContext(SelectionSource.Unknown, null, default, default);

        [Header("Selectable")]
        [SerializeField]
        private bool isSelectable = true;

        [SerializeField]
        private bool toggleOnSelect = true;

        [Header("Instruction Prompt")]
        [SerializeField]
        [TextArea(1, 3)]
        private string instructionText;

        [SerializeField]
        [TextArea(1, 3)]
        private string instructionTextTurkish;

        [Header("Instruction Placement")]
        [Tooltip("Local offset from door pivot (or this transform when pivot is empty).")]
        [SerializeField]
        private Vector3 instructionLocalOffset = new Vector3(0f, 2f, 0f);

        [Tooltip("Popup size multiplier (1 = InteractHoverPopupHost default scale).")]
        [SerializeField]
        [Min(0.01f)]
        private float instructionPopupScale = 1f;

        [Header("Door Pivot")]
        [SerializeField]
        private Transform doorPivot;

        [Header("Rotation")]
        [SerializeField]
        private SelectableDoorOpenDirection openDirection = SelectableDoorOpenDirection.PositiveY;

        [SerializeField]
        private float openAngle = 90f;

        [SerializeField]
        private float duration = 0.35f;

        [Header("Initial State")]
        [SerializeField]
        private bool startOpen;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onSelected = new UnityEvent();

        [SerializeField]
        private UnityEvent onOpened = new UnityEvent();

        [SerializeField]
        private UnityEvent onClosed = new UnityEvent();

        private Transform _pivot;
        private Quaternion _closedLocalRotation;
        private Coroutine _rotationRoutine;
        private bool _isOpen;
        private InstructionPromptController _instructionPrompt;

        public bool IsSelectable => isSelectable;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _pivot = doorPivot != null ? doorPivot : transform;
            _closedLocalRotation = _pivot.localRotation;

            if (startOpen)
            {
                _pivot.localRotation = GetOpenLocalRotation(DefaultSelectionContext);
                _isOpen = true;
            }
            else
            {
                _isOpen = false;
            }

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
            EnsureInstructionPrompt();
            _instructionPrompt?.SetHovered(isHovered);
        }

        public void SetSelectable(bool value)
        {
            isSelectable = value;
        }

        public void Open()
        {
            Open(DefaultSelectionContext);
        }

        public void Open(SelectionContext context)
        {
            Quaternion target = GetOpenLocalRotation(context);
            StartRotation(target, true);
        }

        public void Close()
        {
            StartRotation(_closedLocalRotation, false);
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open(DefaultSelectionContext);
            }
        }

        public void AddOpenedListener(UnityAction listener)
        {
            if (listener == null || onOpened == null)
            {
                return;
            }

            onOpened.AddListener(listener);
        }

        public void RemoveOpenedListener(UnityAction listener)
        {
            if (listener == null || onOpened == null)
            {
                return;
            }

            onOpened.RemoveListener(listener);
        }

        public void Select(SelectionContext context)
        {
            if (!isSelectable)
            {
                return;
            }

            if (onSelected != null)
            {
                onSelected.Invoke();
            }

            if (toggleOnSelect)
            {
                if (_isOpen)
                {
                    Close();
                }
                else
                {
                    Open(context);
                }
            }
            else
            {
                Open(context);
            }
        }

        private Quaternion GetOpenLocalRotation(SelectionContext context)
        {
            if (openDirection == SelectableDoorOpenDirection.AwayFromInteractor ||
                openDirection == SelectableDoorOpenDirection.TowardInteractor)
            {
                // Swing around local Y; sign from which side of the door (local +X) the interactor stands.
                // No interactor: match PositiveY.
                float sign = 1f;
                if (context.Interactor != null)
                {
                    Vector3 localInteractor = _pivot.InverseTransformPoint(context.Interactor.position);
                    sign = localInteractor.x >= 0f ? 1f : -1f;
                }
                else if (context.Hit.collider != null)
                {
                    Vector3 localHit = _pivot.InverseTransformPoint(context.Hit.point);
                    sign = localHit.x >= 0f ? 1f : -1f;
                }

                if (openDirection == SelectableDoorOpenDirection.TowardInteractor)
                {
                    sign = -sign;
                }

                return _closedLocalRotation * Quaternion.Euler(0f, openAngle * sign, 0f);
            }

            switch (openDirection)
            {
                case SelectableDoorOpenDirection.PositiveY:
                    return _closedLocalRotation * Quaternion.Euler(0f, openAngle, 0f);
                case SelectableDoorOpenDirection.NegativeY:
                    return _closedLocalRotation * Quaternion.Euler(0f, -openAngle, 0f);
                case SelectableDoorOpenDirection.PositiveX:
                    return _closedLocalRotation * Quaternion.Euler(openAngle, 0f, 0f);
                case SelectableDoorOpenDirection.NegativeX:
                    return _closedLocalRotation * Quaternion.Euler(-openAngle, 0f, 0f);
                case SelectableDoorOpenDirection.PositiveZ:
                    return _closedLocalRotation * Quaternion.Euler(0f, 0f, openAngle);
                case SelectableDoorOpenDirection.NegativeZ:
                    return _closedLocalRotation * Quaternion.Euler(0f, 0f, -openAngle);
                default:
                    return _closedLocalRotation * Quaternion.Euler(0f, openAngle, 0f);
            }
        }

        private void StartRotation(Quaternion targetLocal, bool opening)
        {
            if (_pivot == null)
            {
                _pivot = doorPivot != null ? doorPivot : transform;
            }

            if (_rotationRoutine != null)
            {
                StopCoroutine(_rotationRoutine);
            }

            _rotationRoutine = StartCoroutine(AnimateRotation(targetLocal, opening));
        }

        private IEnumerator AnimateRotation(Quaternion targetLocal, bool opening)
        {
            Quaternion start = _pivot.localRotation;
            float elapsed = 0f;
            float dur = Mathf.Max(0.0001f, duration);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                _pivot.localRotation = Quaternion.Slerp(start, targetLocal, t);
                yield return null;
            }

            _pivot.localRotation = targetLocal;
            _rotationRoutine = null;
            _isOpen = opening;

            if (opening)
            {
                if (onOpened != null)
                {
                    onOpened.Invoke();
                }
            }
            else
            {
                if (onClosed != null)
                {
                    onClosed.Invoke();
                }
            }
        }

        private void EnsureInstructionPrompt()
        {
            if (string.IsNullOrWhiteSpace(instructionText) && string.IsNullOrWhiteSpace(instructionTextTurkish))
            {
                return;
            }

            if (_instructionPrompt == null)
            {
                _instructionPrompt = new InstructionPromptController(
                    this,
                    resolveAnchor: () => doorPivot != null ? doorPivot : transform,
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
    }
}
