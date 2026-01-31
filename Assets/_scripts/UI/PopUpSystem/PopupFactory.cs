using UnityEngine;

namespace Woi.PopUpSystem
{
    [CreateAssetMenu(fileName = "PopupFactory", menuName = "SO/Popup/PopupFactory")]
    public class PopupFactory : ScriptableObject
    {
        public enum PlatformType
        {
            PC,
            VR
        }
    
        [SerializeField] private Popup2D popup2DPrefab;
        [SerializeField] private Popup2D hazardPopup;
        //[SerializeField] private PopupVR popupVRPrefab;
        
        [SerializeField] private PlatformType currentPlatform;
        
        public BasePopup CreatePopup(Transform popupContainer, bool isHazard)
        {
            Popup2D popupPrefab = isHazard ? hazardPopup  : popup2DPrefab;
            
            //Pool
            BasePopup popup = Instantiate(popupPrefab, popupContainer);
            return popup;
        }
    }
}
