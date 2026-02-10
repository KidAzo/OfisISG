using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Woi.Events;

namespace Woi.HazardSystem
{
    public class HazardResultController : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [Inject] IGameManager gameManager;
        bool _usedThisGame;
        int _lastClickFrame = -1;

        [SerializeField] HazardSystemUIController _uiController;
        SceneTimer _sceneTimer;

        void Start()
        {
            _sceneTimer = FindFirstObjectByType<SceneTimer>();
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

            _uiController.BuildReport(gameManager.GetGameSettings().PlayerName,
            _sceneTimer.GetElapsedTime(),
            result,
            System.DateTime.Now);
             
             await UniTask.NextFrame();

            _uiController.gameObject.SetActive(true);
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

