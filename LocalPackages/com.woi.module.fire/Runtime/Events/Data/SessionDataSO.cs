using System.Collections.Generic;
using UnityEngine;
using FireExtinguisher.Core;

namespace Woi.Events.Data
{
    /// <summary>
    /// Global data carrier to pass session parameters between scenes (e.g. from UI to Gameplay).
    /// </summary>
    [CreateAssetMenu(fileName = "SessionData", menuName = "WOI/Session Data")]
    public class SessionDataSO : ScriptableObject
    {
        public List<FireClass> SelectedClasses = new List<FireClass>();
        public string UserName;
        public string UserId;

        public event System.Action OnSessionUpdated;

        public void NotifyUpdated()
        {
            OnSessionUpdated?.Invoke();
        }
    }
}
