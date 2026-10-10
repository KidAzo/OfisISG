using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Blocks <see cref="SelectionSystemManager"/> while the waste bin menu or result screen is open.
    /// The correct/wrong explanation popup does not block selection so the player can move on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteSelectionInputGate : MonoBehaviour, ISelectionInputGate
    {
        [SerializeField] private WasteSelectionMenu selectionMenu;
        [SerializeField] private WasteResultScreenController resultScreen;
        [SerializeField] private WasteExplanationPopup explanationPopup;

        public bool CanSelect
        {
            get
            {
                if (resultScreen != null && resultScreen.IsVisible)
                    return false;

                if (selectionMenu != null && selectionMenu.IsVisible)
                    return false;

                return true;
            }
        }

        private void Awake()
        {
            if (selectionMenu == null)
                selectionMenu = GetComponent<WasteSelectionMenu>();

            if (resultScreen == null)
                resultScreen = GetComponent<WasteResultScreenController>();

            if (explanationPopup == null)
                explanationPopup = GetComponent<WasteExplanationPopup>();
        }
    }
}
