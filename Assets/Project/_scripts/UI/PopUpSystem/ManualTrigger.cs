namespace Woi.PopUpSystem
{
    public class ManualTrigger : PopupTrigger
    {
        protected override void InitializeTrigger()
        {
            // Manuel trigger, dışarıdan çağrılacak
        }

        public void Trigger()
        {
            TriggerPopup();
        }
    }
}