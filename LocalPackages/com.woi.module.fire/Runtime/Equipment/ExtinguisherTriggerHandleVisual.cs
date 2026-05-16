using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// Animates the top (squeeze) lever mesh when the player holds the spray input while this
    /// extinguisher is equipped. Releases back to the prefab-authored rest pose when input or equip ends.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Equipment/Extinguisher Trigger Handle Visual")]
    public sealed class ExtinguisherTriggerHandleVisual : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to rotate (e.g. UpHandle_low). Leave empty to find a child named UpHandle_low.")]
        [SerializeField]
        Transform handleTransform;

        [Header("Pose")]
        [Tooltip("Extra local rotation applied while spray input is held (degrees, applied as Euler before rest). Tune until the lever sits parallel with the lower handle.")]
        [SerializeField]
        Vector3 pressedLocalEulerOffset = new Vector3(42f, 0f, 0f);

        [Tooltip("How fast the handle blends toward pressed / rest (higher = snappier).")]
        [SerializeField, Min(0f)]
        float blendSpeed = 22f;

        ExtinguisherPickupItem _pickup;
        ISprayInputProvider _sprayInput;
        Quaternion _restLocal;
        float _pressedBlend;

        void Awake()
        {
            if (handleTransform == null)
                handleTransform = FindDeepChild(transform, "UpHandle_low");

            if (handleTransform != null)
                _restLocal = handleTransform.localRotation;

            _pickup = GetComponent<ExtinguisherPickupItem>();
            foreach (MonoBehaviour mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ISprayInputProvider sip)
                {
                    _sprayInput = sip;
                    break;
                }
            }
        }

        void OnDisable()
        {
            if (handleTransform != null)
                handleTransform.localRotation = _restLocal;
            _pressedBlend = 0f;
        }

        void LateUpdate()
        {
            if (handleTransform == null)
                return;

            bool equipped = _pickup != null && _pickup.IsEquipped;
            bool sprayHeld = equipped && _sprayInput != null && _sprayInput.IsSprayHeld;

            float target = sprayHeld ? 1f : 0f;
            float k = blendSpeed <= 0f ? 1f : 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            _pressedBlend = Mathf.Lerp(_pressedBlend, target, k);

            Quaternion pressed = _restLocal * Quaternion.Euler(pressedLocalEulerOffset);
            handleTransform.localRotation = Quaternion.Slerp(_restLocal, pressed, _pressedBlend);
        }

        static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                    return t;
            }

            return null;
        }
    }
}
