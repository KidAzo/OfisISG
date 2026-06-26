using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// World-placed fire blanket that can be equipped to the player hand anchor (PC: E + crosshair).
    /// Hover outline uses the same <see cref="PCHoverInteractor"/> + <see cref="IHoverable"/> path as <see cref="Alarm"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Fire Blanket Pickup Item")]
    public sealed class FireBlanketPickupItem : MonoBehaviour, IHoverable
    {
        [Header("Drop")]
        [SerializeField]
        private Transform dropAnchor;

        [Header("Display")]
        [SerializeField]
        private string displayName = "Fire Blanket";

        [Header("Hover Outline")]
        [SerializeField]
        private Outline outline;

        [SerializeField]
        private bool useOutlineWidth = true;

        [SerializeField, Min(0f)]
        private float hoverOutlineWidth = 5f;

        private Collider[] _colliders;
        private float _defaultOutlineWidth;
        private bool _isHovered;

        public string DisplayName => displayName;
        public bool IsEquipped { get; private set; }
        public bool IsConsumed { get; private set; }

        /// <summary>Used by <see cref="VRHandFireBlanketGrabber"/> (VR grip, no PC equip anchor).</summary>
        internal void SetEquippedState(bool equipped)
        {
            IsEquipped = equipped;
        }

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            RemoveLegacyHoverComponentsImmediate();
            EnsurePickupCollider();
        }

        private void Start()
        {
            EnsureOutline();
            ApplyOutlineHover(false);
        }

        public void Hover(bool isHovered)
        {
            if (IsEquipped || IsConsumed)
            {
                if (_isHovered)
                {
                    ApplyOutlineHover(false);
                }

                return;
            }

            if (_isHovered == isHovered)
            {
                return;
            }

            _isHovered = isHovered;
            ApplyOutlineHover(isHovered);
        }

        public bool EquipToPlayer(Transform equipAnchor)
        {
            if (IsConsumed)
            {
                return false;
            }

            if (IsEquipped)
            {
                Debug.LogWarning($"[FireBlanketPickupItem] '{name}' is already equipped.", this);
                return false;
            }

            if (!IsUsableWorldParent(equipAnchor, out string reason))
            {
                Debug.LogError(
                    $"[FireBlanketPickupItem] Cannot equip '{name}' — invalid anchor ({reason}).",
                    this);
                return false;
            }

            Hover(false);
            SetWallPromptActive(false);
            IsEquipped = true;
            transform.SetParent(equipAnchor, worldPositionStays: false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            SetCollidersEnabled(false);
            return true;
        }

        public void DropFromPlayer()
        {
            if (!IsEquipped || IsConsumed)
            {
                return;
            }

            IsEquipped = false;
            transform.SetParent(null);

            if (dropAnchor != null)
            {
                transform.SetPositionAndRotation(dropAnchor.position, dropAnchor.rotation);
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            SetCollidersEnabled(true);
            SetWallPromptActive(true);
        }

        /// <summary>
        /// Hides the equipped blanket after it is placed on the fire.
        /// </summary>
        public void ConsumeOnFire()
        {
            if (IsConsumed)
            {
                return;
            }

            Hover(false);
            IsConsumed = true;
            IsEquipped = false;
            gameObject.SetActive(false);
        }

        private void EnsurePickupCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.isTrigger = false;
            box.enabled = true;

            if (TryFitBoxColliderToRenderers(box))
            {
                return;
            }

            box.center = Vector3.zero;
            box.size = new Vector3(0.5f, 0.08f, 0.5f);
        }

        private bool TryFitBoxColliderToRenderers(BoxCollider box)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            if (localSize.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            box.center = localCenter;
            box.size = localSize;
            return true;
        }

        private void EnsureOutline()
        {
            if (outline == null)
            {
                outline = GetComponent<Outline>();
            }

            if (outline == null)
            {
                outline = GetComponentInChildren<Outline>(true);
            }

            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.OutlineColor = new Color(1f, 0.92f, 0f, 1f);
                outline.OutlineWidth = 2f;
            }

            if (_defaultOutlineWidth <= 0f)
            {
                _defaultOutlineWidth = outline.OutlineWidth;
            }
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

        private void RemoveLegacyHoverComponentsImmediate()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "HoverableOutline" || typeName == "HoverOutline" || typeName == "SelectableInstructionPrompt")
                {
                    DestroyImmediate(behaviour);
                }
            }
        }

        private static bool IsUsableWorldParent(Transform anchor, out string reason)
        {
            reason = string.Empty;
            if (anchor == null)
            {
                reason = "null";
                return false;
            }

            if (!anchor.gameObject.activeInHierarchy)
            {
                reason = "inactive hierarchy";
                return false;
            }

            Vector3 position = anchor.position;
            if (!IsFiniteVector3(position))
            {
                reason = "non-finite position";
                return false;
            }

            Quaternion rotation = anchor.rotation;
            if (!IsFiniteQuaternion(rotation))
            {
                reason = "non-finite rotation";
                return false;
            }

            Vector3 lossyScale = anchor.lossyScale;
            if (!IsFiniteVector3(lossyScale) || lossyScale.x < 1e-6f || lossyScale.y < 1e-6f || lossyScale.z < 1e-6f)
            {
                reason = "non-finite or ~zero lossyScale";
                return false;
            }

            return true;
        }

        private static bool IsFiniteVector3(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFiniteQuaternion(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) && float.IsFinite(value.w);

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = enabled;
                }
            }
        }

        private void SetWallPromptActive(bool active)
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "SelectableInstructionPrompt")
                {
                    continue;
                }

                behaviour.SendMessage("SetWallPromptActive", active, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
