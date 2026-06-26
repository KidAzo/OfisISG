using System;
using System.Collections;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Schedules trigger overlap refresh after player teleport. Implementation lives in Scenario assembly (see <see cref="OfficeFireTriggerVolumeRefreshBridge"/>).
    /// </summary>
    public static class OfficeFirePlayerTriggerRefresh
    {
        public static event Action RefreshRequested;

        public static void ScheduleAfterPlayerTeleport(MonoBehaviour host)
        {
            if (host == null)
            {
                RequestRefresh();
                return;
            }

            host.StartCoroutine(RefreshAfterPhysicsSettles());
        }

        public static void RequestRefresh() => RefreshRequested?.Invoke();

        static IEnumerator RefreshAfterPhysicsSettles()
        {
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            RequestRefresh();
        }
    }
}
