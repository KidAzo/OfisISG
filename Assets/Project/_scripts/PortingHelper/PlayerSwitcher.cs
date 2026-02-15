using Reflex.Attributes;
using UnityEngine;

namespace Woi.Porting
{
    public class PlayerSwitcher : MonoBehaviour, IModeParticipant
    {
        [SerializeField] GameObject pcPlayer;
        [SerializeField] GameObject xrPlayer;
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

            pcPlayer.SetActive(!isVr);
            xrPlayer.SetActive(isVr);
        }

        public void OnBeforeModeChange(AppMode from, AppMode to) {}
    }
}
