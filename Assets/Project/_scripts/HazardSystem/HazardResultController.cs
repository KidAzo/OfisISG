using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Woi.HazardSystem
{
    public class HazardResultController : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;
        [SerializeField] private Button button;

        bool _usedThisGame;
        int _lastClickFrame = -1;

        [Button]
        private void GetHazardResult()
        {
            var result = hazardManagerService.BuildHazardCheckResult();

            foreach (var checkable in result.foundedChecks)
            {
                Debug.Log($"HazardName: {checkable.TaskName}");
            }

            foreach (var checkable in result.missedChecks)
            {
                Debug.Log($"Missed HazardName: {checkable.TaskName}");
            }

            Debug.Log($"Score Ratio: {result.Score}");
        }

        public void GetCvsDatas()
        {
            if (Time.frameCount == _lastClickFrame)
                return;

            _lastClickFrame = Time.frameCount;

            if (_usedThisGame)
                return;

            _usedThisGame = true;

            button.interactable = false;

            GetHazardResult();
        }
    }
}

