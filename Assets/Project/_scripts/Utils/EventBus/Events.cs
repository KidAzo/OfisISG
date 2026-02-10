using UnityEngine;
using UnityEngine.Serialization;
using WoiUtils.AudioSystem;

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
        public SoundDefinition soundDefinition;
        public string hazardTitle;
        public string description;
        public int score;
        public int hazardID;

        public OnHazardFixed(string hazardTitle, string description, int score, SoundDefinition soundDefinition, int hazardID)
        {
            this.hazardTitle = hazardTitle;
            this.score = score;
			this.description = description;
            this.soundDefinition = soundDefinition;
            this.hazardID = hazardID;
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