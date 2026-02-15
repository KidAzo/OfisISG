using UnityEngine;
using Obvious.Soap;
using Woi.DataHandler;
using Woi.Events;

namespace Woi.HazardSystem
{
    public class HazardResultFinisher : MonoBehaviour
    {
        [SerializeField] ScriptableEventNoParam onHazardResultFinished;
        [SerializeField] GetCvsOutput getCvsOutput;
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
            getCvsOutput.ExportHazardData();
        }
    }

    public struct OnXRHazardResultFinished : IEvent
    {
        public Vector3 position;

        public OnXRHazardResultFinished(Vector3 position)
        {
            this.position = position;
        }
    }
}
