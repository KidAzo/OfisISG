using System.Collections;
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
        [SerializeField ]TeleportationProvider teleportationProvider;
        [SerializeField] XRRayInteractor xRRayInteractor;
        [SerializeField] Camera playerCamera;
        [Inject] IXRPlayerService xrPlayerService;
        [Inject] IPortingService  portingService;
    
        void Awake()
        {
            characterController = GetComponent<CharacterController>();
           
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
                characterController.enabled = false;

                Vector3 delta = targetPosition - playerCamera.transform.position;
                transform.position += delta;

                characterController.enabled = true;
        }

        public void OnLevelFinished(OnXRHazardResultFinished eventData)
        {
            TeleportationState(false);
            StartCoroutine(TeleportAfterXRStable(eventData.position));
        }

        private IEnumerator TeleportAfterXRStable(Vector3 targetWorldPos)
        {
            yield return new WaitForSeconds(1f); 
 
            yield return null;
            yield return null;
            yield return null;

            Teleport(targetWorldPos);

            yield return null;
            yield return null;
            yield return null;
            RotatePlayer();
        }

        void TeleportationState(bool state)
        {
            teleportationProvider.enabled = state;
        }

        void RotatePlayer()
        {
            transform.rotation = Quaternion.identity; 
    }
}
}

