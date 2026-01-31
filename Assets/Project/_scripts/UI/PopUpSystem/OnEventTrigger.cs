namespace Woi.PopUpSystem
{
    public class OnEventTrigger : PopupTrigger
    {
        protected override void InitializeTrigger()
        {
            // Event sistemine subscribe ol
            ///eventBus.Subscribe(popupData.eventName, OnEventReceived);
        }

        private void OnEventReceived()
        {
            //TriggerPopup();
        }
    }
}