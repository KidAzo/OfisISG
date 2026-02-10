using UnityEngine;
using Obvious.Soap;
using Woi.DataHandler;

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
}
