using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
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

    public struct OnLogged : IEvent
    {
        public string playerName;
        public int playerID;
        public int language;

        public OnLogged(string playerName, int playerID, int language)
        {
            this.playerName = playerName;
            this.playerID = playerID;
            this.language = language;
        }
    }

    public struct OnLanguageChanged : IEvent
    {
        public int language;

        public OnLanguageChanged(int language)
        {
            this.language = language;
        }
    }
}