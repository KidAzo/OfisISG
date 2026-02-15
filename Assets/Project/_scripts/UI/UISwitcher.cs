using Reflex.Attributes;
using UnityEngine;

namespace Woi.Porting
{
    public class UISwitcher : MonoBehaviour, IModeParticipant
    {
        [SerializeField] GameObject pcCanvas;
        [SerializeField] GameObject vrCanvas;

        [Inject] IPortingService portingService;

        void OnEnable()
        {
            portingService.Register(this);
        }

        void OnDisable()
        {
            portingService.Unregister(this);
        }

        public void OnAfterModeChange(AppMode mode)
        {
            bool isVr = mode == AppMode.VR;

            pcCanvas.SetActive(!isVr);
            vrCanvas.SetActive(isVr);
        }

        public void OnBeforeModeChange(AppMode from, AppMode to)
        {
            
        }
    }
}
