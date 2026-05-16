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
    }
}
