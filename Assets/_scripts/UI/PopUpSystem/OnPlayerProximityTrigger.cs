using UnityEngine;

namespace Woi.PopUpSystem
{
    public class OnPlayerProximityTrigger : PopupTrigger
    {
		//XRPlayerView player;
        [SerializeField] private bool triggerOnce;

        protected override void Start()
        {
            //player = XRPlayerView.Instance;
        }

        private void Update()
        {
            // if (triggerOnce && hasTriggered) return;

            // //float distance = Vector3.Distance(transform.position, player.transform.position);
            
            // foreach (var data in popupDatas)
            // {
            //     if (distance <= data.proximityDistance)
            //     {
            //         TriggerPopup();
            //     }
            // }
        }

        protected override void InitializeTrigger()
        {
            Debug.Log("Worked");
        }
    }
}