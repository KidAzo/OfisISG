using UnityEngine;
using UnityEngine.InputSystem;

public class InteractableController : MonoBehaviour
{
    [SerializeField] LayerMask interactableLayerMask;
    RayInteractor<IInteractable> rayInteractor;
    [SerializeField] Camera cam;

    void Start()
    {
        rayInteractor = new RayInteractor<IInteractable>(new ScreenCenterRayProvider(cam), new PhysicsRaycastService(), new RaySelector());
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
