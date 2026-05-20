using Obvious.Soap;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Archive alarm interactable: hover outline + E to dispatch <c>press_alarm</c> and raise a SOAP event.
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

        private float _defaultOutlineWidth;
        private bool _isHovered;
        private bool _loggedMissingOutline;
        private bool _alarmTriggered;

        public bool IsSelectable => isSelectable && (!_alarmTriggered) && (!requireHoverToPress || _isHovered);

        private void Awake()
        {
            RemoveLegacySelectableAction();
        }

        private void Start()
        {
            EnsureOutline();
            if (outline == null)
            {
                return;
            }

            _defaultOutlineWidth = outline.OutlineWidth;
            ApplyHoverState(false);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Alarm] Ready on '{name}'. Outline='{outline.name}', defaultWidth={_defaultOutlineWidth}, requireHoverToPress={requireHoverToPress}.",
                    this);
            }
        }

        public void Hover(bool isHovered)
        {
            if (_isHovered == isHovered)
            {
                return;
            }

            _isHovered = isHovered;
            EnsureOutline();

            if (outline == null)
            {
                if (!_loggedMissingOutline)
                {
                    Debug.LogWarning(
                        $"[Alarm] Hover({isHovered}) — Quick Outline not found on '{name}' or children (selection still uses hover state).",
                        this);
                    _loggedMissingOutline = true;
                }

                return;
            }

            ApplyHoverState(isHovered);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Alarm] Hover {(isHovered ? "ENTER" : "EXIT")} on '{name}'. outline.enabled={outline.enabled}, width={outline.OutlineWidth}.",
                    this);
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
        }

        private void ApplyHoverState(bool isHovered)
        {
            if (outline == null)
            {
                return;
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
