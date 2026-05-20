using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Starts and stops evacuation NPCs on <see cref="EvacuationPathFollower"/> instances.
    /// Wire from <see cref="ArchiveRoomScenarioController"/> when entering assembly area state.
    /// </summary>
    public sealed class EvacuationNpcDirector : MonoBehaviour
    {
        [SerializeField]
        private bool collectFollowersFromChildren = true;

        [SerializeField]
        private List<EvacuationPathFollower> followers = new List<EvacuationPathFollower>();

        [SerializeField]
        private bool stopAndResetOnStop = true;

        private bool _isEvacuationActive;

        public bool IsEvacuationActive => _isEvacuationActive;

        public void StartEvacuation()
        {
            EnsureFollowerList();

            for (int i = 0; i < followers.Count; i++)
            {
                EvacuationPathFollower follower = followers[i];
                if (follower == null)
                {
                    continue;
                }

                follower.Begin();
            }

            _isEvacuationActive = true;
        }

        public void StopEvacuation()
        {
            EnsureFollowerList();

            for (int i = 0; i < followers.Count; i++)
            {
                EvacuationPathFollower follower = followers[i];
                if (follower == null)
                {
                    continue;
                }

                follower.StopEvacuation(stopAndResetOnStop);
            }

            _isEvacuationActive = false;
        }

        public void RefreshFollowerList()
        {
            followers.Clear();
            if (!collectFollowersFromChildren)
            {
                return;
            }

            followers.AddRange(GetComponentsInChildren<EvacuationPathFollower>(true));
        }

        private void EnsureFollowerList()
        {
            if (collectFollowersFromChildren || followers.Count == 0)
            {
                RefreshFollowerList();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (collectFollowersFromChildren)
            {
                RefreshFollowerList();
            }
        }
#endif
    }
}
