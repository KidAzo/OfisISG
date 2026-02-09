using UnityEngine;
using Obvious.Soap;

namespace Woi.HazardSystem
{
    public class HazardResultFinisher : MonoBehaviour
    {
        [SerializeField] ScriptableEventNoParam onHazardResultFinished;
        HazardResultController hazardResultController;

        void Start()
        {
            hazardResultController = GetComponent<HazardResultController>();
        }

        void OnEnable()
        {
            onHazardResultFinished.OnRaised += OnHazardResultFinished;
        }

        void OnDisable()
        {
            onHazardResultFinished.OnRaised -= OnHazardResultFinished;
        }

        void OnHazardResultFinished()
        {
            hazardResultController.GetHazardResult();
        }
    }
}
