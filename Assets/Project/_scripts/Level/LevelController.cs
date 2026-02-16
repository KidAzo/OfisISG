using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using Woi.Events;
using Woi.HazardSystem;
using Woi.Porting;

namespace Woi.Level
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] Transform playerResultTransform;
        [Inject] IPortingService portingService;
        [Inject] IHazardManagerService hazardManagerService;
        ILevelController levelController;
        [SerializeField] ScriptableEventNoParam onLevelFinished;
        
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


