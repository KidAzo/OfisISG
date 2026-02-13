using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Player;
using Reflex.Attributes;

public class InteractableController : MonoBehaviour
{
    [Inject] IPlayerService playerService;
    [SerializeField] LayerMask interactableLayerMask;
    RayInteractor<IRayTarget> rayInteractor;
    [SerializeField] float interactDistance = 5f;

    void Start()
    {
        rayInteractor = new RayInteractor<IRayTarget>(new ScreenCenterRayProvider(playerService.playerCamera), 
        new PhysicsRaycastService(), 
        new RaySelector());
    }

    void Update()
    {
         if (Mouse.current.leftButton.wasPressedThisFrame)
         {
              if (rayInteractor.TryGetTarget(interactDistance, interactableLayerMask, out IRayTarget target))
              {
                    if (target is IInteractable interactable)
                    {
                        interactable.Interact();
                    }
              }
         }
    }
}
