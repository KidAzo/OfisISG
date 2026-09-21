using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Woi.Player;
using Obvious.Soap;
using WOI.Modules.SDK;

public class InteractableController : MonoBehaviour
{
    [SerializeField] LayerMask interactableLayerMask;
    [SerializeField] float interactDistance = 5f;
    [SerializeField] ScriptableEventNoParam onInteractVr;

    RayInteractor<IRayTarget> rayInteractor;

    void Start()
    {
        SetRayType();
    }

    void OnEnable()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR && onInteractVr != null)
            onInteractVr.OnRaised += InteractWithController;
    }

    void OnDisable()
    {
        if (onInteractVr != null)
            onInteractVr.OnRaised -= InteractWithController;
    }

    void InteractWithController()
    {
        TryInteract();
    }

    void SetRayType()
    {
        bool isXrMode = FirePlatformRuntime.CurrentMode == AppMode.XR;
        IRayProvider provider;

        if (isXrMode)
        {
            XRRayInteractor xrRay = null;
            if (ServiceLocator.TryGet(out IXRPlayerService xrPlayerService) && xrPlayerService != null)
                xrRay = xrPlayerService.XrRayInteractor;
            if (xrRay == null)
                xrRay = FindFirstObjectByType<XRRayInteractor>(FindObjectsInactive.Include);

            if (xrRay != null)
            {
                provider = new XrRayProvider(xrRay);
            }
            else
            {
                var cam = ResolvePcCamera();
                if (cam == null)
                    return;
                provider = new ScreenCenterRayProvider(cam);
            }
        }
        else
        {
            var cam = ResolvePcCamera();
            if (cam == null)
                return;
            provider = new ScreenCenterRayProvider(cam);
        }

        rayInteractor = new RayInteractor<IRayTarget>(
            provider,
            new PhysicsRaycastService(),
            new RaySelector());
    }

    static Camera ResolvePcCamera()
    {
        if (ServiceLocator.TryGet(out IPlayerService playerService)
            && playerService != null
            && playerService.playerCamera != null)
            return playerService.playerCamera;

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null && player.playerCamera != null)
            return player.playerCamera;

        return Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        TryInteract();
    }

    void TryInteract()
    {
        if (rayInteractor == null)
            SetRayType();
        if (rayInteractor == null)
            return;

        if (rayInteractor.TryGetTarget(interactDistance, interactableLayerMask, out IRayTarget target)
            && target is IInteractable interactable)
        {
            interactable.Interact();
        }
    }
}
