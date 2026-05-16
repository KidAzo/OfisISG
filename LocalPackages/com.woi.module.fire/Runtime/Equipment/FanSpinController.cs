using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// Rotates this transform continuously around its local Z axis with
    /// configurable acceleration and deceleration.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/Fan Spin Controller")]
    public sealed class FanSpinController : MonoBehaviour
    {
        [Tooltip("Degrees per second on the local Z axis. Negative = reverse direction.")]
        [SerializeField] private float _speed = 360f;

        private void Update()
        {
           transform.Rotate(0f, 0f, _speed * Time.deltaTime, Space.World);
        }
    }
}
