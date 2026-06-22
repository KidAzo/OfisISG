using System;
using System.Collections.Generic;

namespace Woi.OfficeFire
{
    [Serializable]
    public class OfficeFireScenarioReport
    {
        public OfficeFireScenarioId scenarioId;
        public float reactionTime;
        public List<OfficeFireCorrectActionId> correctActions = new List<OfficeFireCorrectActionId>();
        public List<OfficeFireMistakeId> mistakes = new List<OfficeFireMistakeId>();
        public bool fireControlled;
        public bool evacuated;
        public bool completed;

        /// <summary>Server room only: door end-state captured when the scenario finishes.</summary>
        public bool hasServerRoomDoorEndState;

        /// <summary>True when ColorDoor (4) is closed at scenario end (correct).</summary>
        public bool serverRoomDoorClosedAtEnd;

        public void AddCorrectAction(OfficeFireCorrectActionId id)
        {
            if (id == OfficeFireCorrectActionId.None)
            {
                return;
            }

            if (!correctActions.Contains(id))
            {
                correctActions.Add(id);
            }
        }

        public void AddMistake(OfficeFireMistakeId id)
        {
            if (id == OfficeFireMistakeId.None)
            {
                return;
            }

            if (!mistakes.Contains(id))
            {
                mistakes.Add(id);
            }
        }

        public void RemoveMistake(OfficeFireMistakeId id)
        {
            if (id == OfficeFireMistakeId.None)
            {
                return;
            }

            mistakes.Remove(id);
        }
    }
}
