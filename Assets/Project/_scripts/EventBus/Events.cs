using UnityEngine;
using UnityEngine.Serialization;

namespace Woi.Events
{
	public struct OnSceneGroupLoaded : IEvent
	{
    }

    public struct OnSceneGroupUnloaded : IEvent
    {
        
    }

    public struct OnLevelLoadingAllComplate : IEvent
    {
        
    }


    public struct OnEscapePerformed : IEvent
    {
        public bool state;

        public OnEscapePerformed(bool state)
        {
            this.state = state;
        }
    }

	public struct OnTruckJobDone : IEvent
	{
        public GameObject truck;

        public OnTruckJobDone(GameObject truck)
        {
            this.truck = truck;
		}
	}

    public struct OnHazardFixed : IEvent
    {
        public string hazardTitle;
        public string description;
        public int score;

        public OnHazardFixed(string hazardTitle, string description, int score)
        {
            this.hazardTitle = hazardTitle;
            this.score = score;
			this.description = description;
		}
	}

    public struct OnHazardResult : IEvent
    {
        public bool state;

        public OnHazardResult(bool state)
        {
            this.state = state;
        }
	}

	public struct OnHazardModeFinished : IEvent
	{
		
	}
}