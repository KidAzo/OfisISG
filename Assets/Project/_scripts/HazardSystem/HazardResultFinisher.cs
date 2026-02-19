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
        bool usedThisGame;

        void Start()
        {
            usedThisGame = false;
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
            if (usedThisGame) return;

            hazardResultController.GetHazardResult();
            getCvsOutput.ExportHazardData();
            usedThisGame = true;
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
