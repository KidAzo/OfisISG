using Reflex.Attributes;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Woi.Events;
using Woi.HazardSystem;
using Woi.Porting;

namespace Woi.Player.XR
{   
    public class XRPlayerController : MonoBehaviour
    {
        CharacterController characterController;
        TeleportationProvider teleportationProvider;
        [SerializeField] XRRayInteractor xRRayInteractor;
        [SerializeField] Camera playerCamera;
        [Inject] IXRPlayerService xrPlayerService;
        [Inject] IPortingService  portingService;
    
        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            teleportationProvider = GetComponentInChildren<TeleportationProvider>();
           
            xrPlayerService.Register(xRRayInteractor, playerCamera, transform);  
        }

        void OnEnable()
        {
            if (portingService.CurrentMode == AppMode.XR)
            {
                EventBus.Subscribe<OnXRHazardResultFinished>(OnLevelFinished);
            }
        }

        void OnDisable()
        {
            if (portingService.CurrentMode == AppMode.XR)
            {
                EventBus.Unsubscribe<OnXRHazardResultFinished>(OnLevelFinished);
            }
        }

        void Teleport(Vector3 targetPosition)
        {
            characterController.enabled = false; // Disable the CharacterController to avoid collision issues
            transform.position = targetPosition; // Move the player to the target position
            characterController.enabled = true; 
            Debug.Log("[XRPlayerController] CharacterController re-enabled");
        }

        public void OnLevelFinished(OnXRHazardResultFinished eventData)
        {
            TeleportationState(false); // Disable teleportation after setting the player's position
            Teleport(eventData.position);
        }

        void TeleportationState(bool state)
        {
            teleportationProvider.enabled = state;
        }
    }
}

