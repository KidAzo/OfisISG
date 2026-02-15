using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Woi.Events;
using Woi.Porting;

namespace Woi.HazardSystem
{
    public class HazardResultController : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [Inject] IGameManager gameManager;
        [Inject] IPortingService portingService;
        
        bool _usedThisGame;
        int _lastClickFrame = -1;

        [SerializeField] HazardSystemUIController _uiControllerPc;
        [SerializeField] HazardSystemUIController _uiControllerXR;
        HazardSystemUIController _uiControllerCurrent;
        SceneTimer _sceneTimer;

        void Start()
        {
            _sceneTimer = FindFirstObjectByType<SceneTimer>();
            _uiControllerCurrent = portingService.CurrentMode == AppMode.XR ? _uiControllerXR : _uiControllerPc;
            _uiControllerCurrent.gameObject.SetActive(false);
        }

        [Button]
        public void GetHazardResult()
        {
            ShowHazardResult().Forget();    
        }

        async UniTaskVoid ShowHazardResult()
        {
            EventBus.Publish(new OnHazardResultRequested());

            var result = hazardManagerService.BuildHazardCheckResult();

            _uiControllerCurrent.BuildReport(gameManager.GetGameSettings().PlayerName,
            _sceneTimer.GetElapsedTime(),
            result,
            System.DateTime.Now);
             
             await UniTask.NextFrame();

            _uiControllerCurrent.gameObject.SetActive(true);
        }
        

        public void GetCvsDatas()
        {
            if (Time.frameCount == _lastClickFrame)
                return;

            _lastClickFrame = Time.frameCount;

            if (_usedThisGame)
                return;

            _usedThisGame = true;

            GetHazardResult();
        }
    }

    public struct OnHazardResultRequested : IEvent
    {

    }
}

