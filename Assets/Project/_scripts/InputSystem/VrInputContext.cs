using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.InputSystem
{
    [CreateAssetMenu(menuName = "Input System/Contexts/VR Context")]
    public class VrInputContext : InputContext
    {
        [SerializeField] private ScriptableEventNoParam onInteractInput;
        [SerializeField] private ScriptableEventNoParam onFinishedGame;

        public override void OnEnter()
        {
            if (inputActions == null)
            {
                Debug.LogError("[VrInputContext] InputActions null!");
                return;
            }
            
            inputActions.VR.Enable();

            inputActions.VR.Interact.performed += OnInteract;
            inputActions.VR.FinishedGame.performed += OnGameplayFinished;
        }

        public override void OnExit()
        {
            if (inputActions == null) return;
            
            inputActions.VR.Disable();
           
            inputActions.VR.Interact.performed -= OnInteract;
            inputActions.VR.FinishedGame.performed -= OnGameplayFinished;
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            Debug.Log("[VrInputContext] Interact input performed");
            onInteractInput?.Raise();
        }

        private void OnGameplayFinished(InputAction.CallbackContext ctx)
        {
            Debug.Log("[VrInputContext] Finished Game input performed");
            onFinishedGame?.Raise();
        }
    }
}



