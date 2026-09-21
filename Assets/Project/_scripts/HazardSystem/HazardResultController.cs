using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Woi.Events;
using Woi.Leaderboard;

namespace Woi.HazardSystem
{
    public class HazardResultController : MonoBehaviour
    {
        bool _usedThisGame;
        int _lastClickFrame = -1;

        [SerializeField] HazardSystemUIController _uiControllerPc;
        [SerializeField] HazardSystemUIController _uiControllerXR;
        HazardSystemUIController _uiControllerCurrent;
        SceneTimer _sceneTimer;
        IHazardManagerService _hazardManagerService;
        IGameManager _gameManager;

        void Start()
        {
            _sceneTimer = FindFirstObjectByType<SceneTimer>();
            _hazardManagerService = FindFirstObjectByType<HazardManager>();
            _gameManager = FindFirstObjectByType<GameManager>();

            BindPlatformResultUi();
        }

        public void AssignPcResultUi(HazardSystemUIController pc)
        {
            if (pc == null || IsVrResultUi(pc))
                return;

            _uiControllerPc = pc;
            ApplyPlatformSelection();
        }

        public void BindPlatformResultUi()
        {
            if (_uiControllerXR == null)
            {
                var found = FindObjectsByType<HazardSystemUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (IsVrResultUi(found[i]))
                    {
                        _uiControllerXR = found[i];
                        break;
                    }
                }
            }

            if (IsVrResultUi(_uiControllerPc))
                _uiControllerPc = null;

            ApplyPlatformSelection();
        }

        void ApplyPlatformSelection()
        {
            bool xr = FirePlatformRuntime.CurrentMode == AppMode.XR;
            _uiControllerCurrent = xr ? _uiControllerXR : _uiControllerPc;

            if (_uiControllerPc != null)
                _uiControllerPc.gameObject.SetActive(false);
            if (_uiControllerXR != null)
                _uiControllerXR.gameObject.SetActive(false);
        }

        static bool IsVrResultUi(HazardSystemUIController ui)
        {
            return ui != null && (ui.gameObject.name.Contains("VR") || ui.gameObject.name.Contains("XR"));
        }

        [Button]
        public void GetHazardResult()
        {
            ShowHazardResult().Forget();
        }

        async UniTaskVoid ShowHazardResult()
        {
            EventBus.Raise(new OnHazardResultRequested());

            if (_hazardManagerService == null)
                _hazardManagerService = FindFirstObjectByType<HazardManager>();
            if (_gameManager == null)
                _gameManager = FindFirstObjectByType<GameManager>();
            if (_sceneTimer == null)
                _sceneTimer = FindFirstObjectByType<SceneTimer>();
            if (_uiControllerCurrent == null)
                BindPlatformResultUi();
            else
                ApplyPlatformSelection();

            var result = _hazardManagerService.BuildHazardCheckResult();
            var settings = _gameManager != null ? _gameManager.GetGameSettings() : default;
            var elapsed = _sceneTimer != null ? _sceneTimer.GetElapsedTime() : System.TimeSpan.Zero;

            if (_uiControllerCurrent != null)
            {
                _uiControllerCurrent.BuildReport(
                    settings.PlayerName,
                    elapsed,
                    result,
                    System.DateTime.Now);
            }

            LeaderboardService.SubmitScore(
                settings.PlayerID.ToString(),
                settings.PlayerName,
                result.Score,
                (float)elapsed.TotalSeconds);

            EventBus.Raise(new OnLeaderboardUpdated());

            await UniTask.NextFrame();

            if (_uiControllerCurrent != null)
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
