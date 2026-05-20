using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Default <see cref="IHoverable"/> that toggles Quick Outline on/off or adjusts outline width.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Hoverable Outline")]
    public sealed class HoverableOutline : MonoBehaviour, IHoverable
    {
        [SerializeField]
        private Outline outline;

        [SerializeField]
        private bool useOutlineWidth;

        [SerializeField]
        [Min(0f)]
        private float hoverOutlineWidth = 5f;

        private float _defaultOutlineWidth;
        private bool _isHovered;

        private void Start()
        {
            if (outline == null)
            {
                outline = GetComponent<Outline>();
            }

            if (outline == null)
            {
                return;
            }

            _defaultOutlineWidth = outline.OutlineWidth;
            ApplyHoverState(false);
        }

        public void Hover(bool isHovered)
        {
            if (_isHovered == isHovered || outline == null)
            {
                return;
            }

            _isHovered = isHovered;
            ApplyHoverState(isHovered);
        }

        private void ApplyHoverState(bool isHovered)
        {
            if (useOutlineWidth)
            {
                outline.enabled = isHovered;
                outline.OutlineWidth = isHovered ? hoverOutlineWidth : _defaultOutlineWidth;
                return;
            }

            outline.enabled = isHovered;
        }
    }
}
