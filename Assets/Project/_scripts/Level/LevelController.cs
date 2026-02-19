using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;
using Woi.HazardSystem;
using Woi.Localization;
using Woi.Porting;
using WoiUtils.AudioSystem;

namespace Woi.Level
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] Transform playerResultTransform;
        [Inject] IPortingService portingService;
        [Inject] IHazardManagerService hazardManagerService;
        ILevelController levelController;
        [SerializeField] ScriptableEventNoParam onLevelFinished;
        [SerializeField] SoundDefinition anoncementSounds;
        [Inject] AudioSystem audioSystem;
        [SerializeField] UnityEvent onTrSelected;
        [SerializeField] UnityEvent onEnSelected;
        [SerializeField] UnityEvent onPcSelected;
        [SerializeField] UnityEvent onXrSelected;


        void OnEnable()
        {
            onLevelFinished.OnRaised += FinishLevel;
        }

        void OnDisable()
        {
            onLevelFinished.OnRaised -= FinishLevel;
        }

        void Start()
        {
            bool isXr = portingService.CurrentMode == AppMode.XR;
            levelController = isXr ? new XrLevelController(hazardManagerService, playerResultTransform.position) : new PcLevelController();
            
            PlayAnnouncementSound();
            
            if (isXr)
            {
                onXrSelected?.Invoke();
            }
            else
            {
                onPcSelected?.Invoke();
            }
        }

        void PlayAnnouncementSound()
        {
            if (portingService.CurrentMode == AppMode.XR) return;
        
            var ctx = PlayContext.Default;
            if (LanguageManager.CurrentLanguage == Language.Turkish)
            {
                ctx = ctx.SetClipIndex(0); 
                onTrSelected?.Invoke();
            }
            else
            {
                ctx = ctx.SetClipIndex(1);  
                onEnSelected?.Invoke(); 
            }            

             audioSystem.Play(anoncementSounds, ctx);
        }

        public void PlayAnnouncementSoundXR()
        {
            var ctx = PlayContext.Default;
            if (LanguageManager.CurrentLanguage == Language.Turkish)
            {
                ctx = ctx.SetClipIndex(0); 
                onTrSelected?.Invoke();
            }
            else
            {
                ctx = ctx.SetClipIndex(1);  
                onEnSelected?.Invoke(); 
            }            

             audioSystem.Play(anoncementSounds, ctx);
        }

        public void FinishLevel()
        {
            levelController.FinishLevel();
        }
    }

    public class XrLevelController : ILevelController
    {
        Vector3 playerTransform;
        IHazardManagerService hazardManagerService;

        public XrLevelController(IHazardManagerService hazardManagerService, Vector3 playerTransform)
        {
            this.hazardManagerService = hazardManagerService;
            this.playerTransform = playerTransform;
        }

        public void FinishLevel()
        {
            hazardManagerService.BuildHazardCheckResult();
            EventBus.Publish(new OnXRHazardResultFinished(playerTransform));
        }
    } 

    public class PcLevelController : ILevelController
    {
        public void FinishLevel()
        {
        }
    }   

    public interface ILevelController
    {
        void FinishLevel();
    }   
}


