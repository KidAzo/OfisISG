using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.Gameplay
{
    public class GameplayController : MonoBehaviour
    {
        [SerializeField] ScriptableEventNoParam onGameplayFinished;
        [Inject] AudioSystem audioSystem;

        void OnEnable()
        {
            onGameplayFinished.OnRaised += OnGameplayFinished;  
        }

        void OnDisable()
        {
            onGameplayFinished.OnRaised -= OnGameplayFinished;
        }

        void OnGameplayFinished()
        {
            audioSystem.StopAll();    
        }
    }
}
