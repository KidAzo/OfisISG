using UnityEngine;
using UnityEngine.InputSystem;
using Woi.Player;
using Reflex.Attributes;
using Woi.Porting;
using Obvious.Soap;

public class InteractableController : MonoBehaviour
{
    [Inject] IPlayerService playerService;
    [Inject] IXRPlayerService xrPlayerService;
    [Inject] IPortingService portingService;
    [SerializeField] LayerMask interactableLayerMask;
    RayInteractor<IRayTarget> rayInteractor;
    [SerializeField] float interactDistance = 5f;
    [SerializeField] ScriptableEventNoParam onInteractVr;

    void Start()
    {
        Debug.Log(portingService.CurrentMode);
        SetRayType();
    }

    void OnEnable()
    {
        if (portingService.CurrentMode == AppMode.XR)
        {
            onInteractVr.OnRaised += InteractWithController;
        }
    }

    void OnDisable()
    {
         if (portingService.CurrentMode == AppMode.XR)
         {
            onInteractVr.OnRaised -= InteractWithController;
         }
    }

    void InteractWithController()
    {
        Debug.Log("Interact event received in InteractableController"); 
        if (rayInteractor.TryGetTarget(interactDistance, interactableLayerMask, out IRayTarget target))
        {
            if (target is IInteractable interactable)
            {
                interactable.Interact();
            }
        }
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
