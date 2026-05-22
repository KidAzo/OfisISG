using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Starts and stops evacuation NPCs on <see cref="SplineNpcController"/> instances.
    /// Wire from <see cref="ArchiveRoomScenarioController"/> when entering assembly area state.
    /// </summary>
    public sealed class EvacuationNpcDirector : MonoBehaviour
    {
        [SerializeField]
        private bool collectControllersFromChildren = true;

        [SerializeField]
        private List<SplineNpcController> npcControllers = new List<SplineNpcController>();

        [SerializeField]
        private bool stopAndResetOnStop = true;

        private bool _isEvacuationActive;

        public bool IsEvacuationActive => _isEvacuationActive;

        public void StartEvacuation()
        {
            EnsureControllerList();

            for (int i = 0; i < npcControllers.Count; i++)
            {
                SplineNpcController controller = npcControllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.Begin();
            }

            _isEvacuationActive = true;
        }

        public void StopEvacuation()
        {
            EnsureControllerList();

            for (int i = 0; i < npcControllers.Count; i++)
            {
                SplineNpcController controller = npcControllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.StopEvacuation(stopAndResetOnStop);
            }

            _isEvacuationActive = false;
        }

        public void RefreshControllerList()
        {
            npcControllers.Clear();
            if (!collectControllersFromChildren)
            {
                return;
            }

            npcControllers.AddRange(GetComponentsInChildren<SplineNpcController>(true));
        }

        private void EnsureControllerList()
        {
            if (collectControllersFromChildren || npcControllers.Count == 0)
            {
                RefreshControllerList();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (collectControllersFromChildren)
            {
                RefreshControllerList();
            }
        }
#endif
    }
}
