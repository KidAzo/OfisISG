using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Player;
using Reflex.Attributes;
using Woi.Porting;

public class InteractableController : MonoBehaviour
{
    [Inject] IPlayerService playerService;
    [Inject] IXRPlayerService xrPlayerService;
    [Inject] IPortingService portingService;
    [SerializeField] LayerMask interactableLayerMask;
    RayInteractor<IRayTarget> rayInteractor;
    [SerializeField] float interactDistance = 5f;

    void Start()
    {
        SetRayType();
    }

    void SetRayType()
    {
        bool isXrMode = portingService.CurrentMode == AppMode.XR;   

        rayInteractor = new RayInteractor<IRayTarget>(isXrMode ? 
        new XrRayProvider(xrPlayerService.XrRayInteractor) : new ScreenCenterRayProvider(playerService.playerCamera), 
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
