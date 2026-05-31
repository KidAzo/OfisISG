using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.SelectionSystem
{
    public class SelectionSystemManager : MonoBehaviour
    {
        [SerializeField] Camera mainCamera;
        SelectionSystemController selectionSystemController;
        SelectionInputController selectionInputController;
        [SerializeField] InputAction mouseLeftClick;

        void Start()
        {
            selectionSystemController = new SelectionSystemController(mainCamera);
            selectionInputController = new SelectionInputController(selectionSystemController, mouseLeftClick);
        
            selectionInputController.Enable();
        }

        void OnDisable()
        {
            selectionInputController?.Disable();
        }

        public void SetSelectionInputEnabled(bool enabled)
        {
            if (selectionInputController == null)
                return;

            if (enabled)
                selectionInputController.Enable();
            else
                selectionInputController.Disable();
        }
    }
}
