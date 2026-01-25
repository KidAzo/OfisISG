using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Player;
using Reflex.Attributes;

public class InteractableController : MonoBehaviour
{
    [Inject] IPlayerService playerService;
    [SerializeField] LayerMask interactableLayerMask;
    RayInteractor<IInteractable> rayInteractor;

    void Start()
    {
        rayInteractor = new RayInteractor<IInteractable>(new ScreenCenterRayProvider(playerService.playerCamera), 
        new PhysicsRaycastService(), 
        new RaySelector());
    }

    void Update()
    {
         if (Mouse.current.leftButton.wasPressedThisFrame)
         {
              if (rayInteractor.TryGetTarget(10f, interactableLayerMask, out RaycastHit hit, out IInteractable interactable))
              {
                    interactable.Interact();
              }
         }
    }
}
