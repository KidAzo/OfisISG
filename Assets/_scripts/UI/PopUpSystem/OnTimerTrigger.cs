using UnityEngine;

namespace Woi.PopUpSystem
{
    public class OnTimerTrigger : PopupTrigger
    {
        private float timer;

        protected override void InitializeTrigger()
        {
            //timer = data[0].delayTime;
        }

        private void Update()
        {
            if (hasTriggered) return;

            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                TriggerPopup();
            }
        }
    }
}