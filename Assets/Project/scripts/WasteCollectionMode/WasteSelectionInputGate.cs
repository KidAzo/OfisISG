using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Blocks <see cref="SelectionSystemManager"/> while waste bin UI or result screen is open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteSelectionInputGate : MonoBehaviour, ISelectionInputGate
    {
        [SerializeField] private WasteSelectionMenu selectionMenu;
        [SerializeField] private WasteResultScreenController resultScreen;

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
        }
    }
}
