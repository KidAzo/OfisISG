using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.InputSystem
{
    [CreateAssetMenu(menuName = "Input System/Contexts/VR Context")]
    public class VrInputContext : InputContext
    {
        [SerializeField] private ScriptableEventNoParam onInteractInput;

        public override void OnEnter()
        {
            if (inputActions == null)
            {
                Debug.LogError("[VrInputContext] InputActions null!");
                return;
            }
            
            inputActions.VR.Enable();

            inputActions.VR.Interact.performed += OnInteract;
        }

        public override void OnExit()
        {
            if (inputActions == null) return;
            
            inputActions.VR.Disable();
           
            inputActions.VR.Interact.performed -= OnInteract;
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            Debug.Log("[VrInputContext] Interact input performed");
            onInteractInput?.Raise();
        }
    }
}



