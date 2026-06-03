using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;
//using Woi.Level;
//using Woi.Porting;

namespace Woi.InputSystem
{
    [CreateAssetMenu(menuName = "Input System/Contexts/VR Context")]
    public class VrInputContext : InputContext
    {
        [SerializeField] private ScriptableEventNoParam onInteractInput;
        [SerializeField] private ScriptableEventNoParam onFinishedGame;
        [SerializeField] private ScriptableEventNoParam preOnGameplayFinishedInput;
        //[SerializeField] private ScriptableEnumPortingVariable portingVariable;

        public ScriptableEventNoParam InteractEvent => onInteractInput;

        public ScriptableEventNoParam PreOnGameplayFinishedEvent => preOnGameplayFinishedInput;

        public override void OnEnter()
        {
            if (inputActions == null)
            {
                Debug.LogError("[VrInputContext] InputActions null!");
                return;
            }
            
            inputActions.XR.Enable();

            inputActions.XR.Interact.performed += OnInteract;
            inputActions.XR.FinishedGame.performed += OnGameplayFinished;
        }

        public override void OnExit()
        {
            if (inputActions == null) return;
            
            inputActions.XR.Disable();
           
            inputActions.XR.Interact.performed -= OnInteract;
            inputActions.XR.FinishedGame.performed -= OnGameplayFinished;
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            onInteractInput?.Raise();
        }

        private void OnGameplayFinished(InputAction.CallbackContext ctx)
        {
            // Sağ grip <see cref="PlayerInputActions.XR.FinishedGame"/> ile <c>Gameplay/ExitPanel</c> aynı düğmeye bağlı;
            // <c>onFinishedGame</c> (onGameplayFinishedInput) burada tetiklenirse panel açılır açılmaz oturum biter ve sonuç ekranı gelir.
            // Oturum sonu: <see cref="Woi.UI.Result.ExitPanelController"/> EVET → <see cref="Woi.Training.LevelController.RequestEndSessionFromExitPanel"/>.
            preOnGameplayFinishedInput?.Raise();
        }
    }
}



