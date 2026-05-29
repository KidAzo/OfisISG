using System;
using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class InstructionPromptController
    {
        private readonly MonoBehaviour _host;
        private readonly Func<Transform> _resolveAnchor;
        private readonly Func<Vector3> _resolveLocalOffset;
        private readonly bool _hideWhenNotSelectable;
        private readonly bool _hideWhenInstructionEmpty;
        private readonly bool _preferTurkish;
        private readonly Outline _outline;
        private readonly bool _useOutlineWidth;
        private readonly float _hoverOutlineWidth;

        private OfficeFireInteractPopupHost _popupHost;
        private float _defaultOutlineWidth;
        private bool _isHovered;
        private string _instructionText = string.Empty;
        private string _instructionTextTurkish = string.Empty;

        public InstructionPromptController(
            MonoBehaviour host,
            Func<Transform> resolveAnchor,
            Func<Vector3> resolveLocalOffset,
            bool hideWhenNotSelectable,
            bool hideWhenInstructionEmpty,
            bool preferTurkish,
            Outline outline,
            bool useOutlineWidth,
            float hoverOutlineWidth)
        {
            _host = host;
            _resolveAnchor = resolveAnchor ?? (() => host != null ? host.transform : null);
            _resolveLocalOffset = resolveLocalOffset ?? (() => Vector3.zero);
            _hideWhenNotSelectable = hideWhenNotSelectable;
            _hideWhenInstructionEmpty = hideWhenInstructionEmpty;
            _preferTurkish = preferTurkish;
            _outline = outline;
            _useOutlineWidth = useOutlineWidth;
            _hoverOutlineWidth = hoverOutlineWidth;

            if (_outline != null)
            {
                _defaultOutlineWidth = _outline.OutlineWidth;
                ApplyOutlineState(false);
            }
        }

        public void SetInstruction(string english, string turkish)
        {
            _instructionText = english ?? string.Empty;
            _instructionTextTurkish = turkish ?? string.Empty;
            RefreshPopup();
        }

        public void SetHovered(bool isHovered)
        {
            if (_isHovered == isHovered)
            {
                return;
            }

            _isHovered = isHovered;
            ApplyOutlineState(isHovered);
            RefreshPopup();
        }

        public void Hide()
        {
            _isHovered = false;
            ApplyOutlineState(false);
            ResolvePopupHost()?.Hide(_host);
        }

        public void Tick()
        {
            if (!_isHovered || _host == null)
            {
                return;
            }

            OfficeFireInteractPopupHost host = ResolvePopupHost();
            if (host == null)
            {
                return;
            }

            host.UpdatePosition(_host, ResolveAnchor(), ResolveLocalOffset());
        }

        private void RefreshPopup()
        {
            string text = ResolveInstructionText();
            bool hasText = !string.IsNullOrWhiteSpace(text);
            bool canShow = _isHovered && hasText;

            if (_hideWhenInstructionEmpty && !hasText)
            {
                canShow = false;
            }

            if (_hideWhenNotSelectable && !IsSelectableActive())
            {
                canShow = false;
            }

            OfficeFireInteractPopupHost host = ResolvePopupHost();
            if (host == null)
            {
                return;
            }

            if (!canShow)
            {
                host.Hide(_host);
                return;
            }

            host.Show(_host, ResolveAnchor(), ResolveLocalOffset(), text);
        }

        private Transform ResolveAnchor()
        {
            Transform anchor = _resolveAnchor();
            return anchor != null ? anchor : _host.transform;
        }

        private Vector3 ResolveLocalOffset() => _resolveLocalOffset();

        private OfficeFireInteractPopupHost ResolvePopupHost()
        {
            if (_popupHost != null && _popupHost.isActiveAndEnabled)
            {
                return _popupHost;
            }

            _popupHost = null;
            if (!OfficeFireInteractPopupHost.TryGetInstance(out _popupHost))
            {
                Debug.LogWarning(
                    "[InstructionPromptController] OfficeFireInteractPopupHost not found in scene. Add InteractHoverPopupHost under UI.",
                    _host);
            }

            return _popupHost;
        }

        private string ResolveInstructionText()
        {
            bool useTurkish = _preferTurkish && !string.IsNullOrWhiteSpace(_instructionTextTurkish);
            if (useTurkish)
            {
                return _instructionTextTurkish;
            }

            return _instructionText;
        }

        private bool IsSelectableActive()
        {
            if (_host == null)
            {
                return true;
            }

            ISelectable[] selectables = _host.GetComponents<ISelectable>();
            if (selectables == null || selectables.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < selectables.Length; i++)
            {
                ISelectable selectable = selectables[i];
                if (selectable != null && selectable.IsSelectable)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyOutlineState(bool isHovered)
        {
            if (_outline == null)
            {
                return;
            }

            if (_useOutlineWidth)
            {
                _outline.enabled = isHovered;
                _outline.OutlineWidth = isHovered ? _hoverOutlineWidth : _defaultOutlineWidth;
                return;
            }

            _outline.enabled = isHovered;
        }
    }
}
