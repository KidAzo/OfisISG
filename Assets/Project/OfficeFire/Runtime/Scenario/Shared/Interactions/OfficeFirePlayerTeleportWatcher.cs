using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Detects large rig position jumps (spawn teleport, assembly point, scene loads) and refreshes all trigger volumes.
    /// Attach to XR Origin / player movement root once — no per-teleport call sites required.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class OfficeFirePlayerTeleportWatcher : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        float teleportDistanceThresholdMeters = 0.75f;

        Vector3 _lastPosition;
        bool _hasLastPosition;

        void OnEnable()
        {
            _lastPosition = transform.position;
            _hasLastPosition = true;
            OfficeFirePlayerTriggerRefresh.ScheduleAfterPlayerTeleport(this);
        }

        void LateUpdate()
        {
            Vector3 current = transform.position;
            if (!_hasLastPosition)
            {
                _lastPosition = current;
                _hasLastPosition = true;
                return;
            }

            if (Vector3.Distance(current, _lastPosition) >= teleportDistanceThresholdMeters)
                OfficeFirePlayerTriggerRefresh.ScheduleAfterPlayerTeleport(this);

            _lastPosition = current;
        }
    }
}
