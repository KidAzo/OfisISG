using Reflex.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Woi.HazardSystem
{
    public class HazardResultController : MonoBehaviour
    {
        [Inject] IHazardManagerService hazardManagerService;

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
    }
}

