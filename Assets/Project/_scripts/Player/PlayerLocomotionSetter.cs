using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.Player
{
    public class PlayerLocomotionSetter : MonoBehaviour
    {
        [Inject] IPlayerService playerService;
        [SerializeField] Transform initialPosition;

        void OnEnable()
        {
            playerService.OnPlayerRegistered += HandlePlayerRegistered;
        }

        void OnDisable()
        {
            playerService.OnPlayerRegistered -= HandlePlayerRegistered;
        }

        void HandlePlayerRegistered()
        {
            playerService.SetPlayerLocomotion(initialPosition.position);
        }

        void Update()
        {
            if(Keyboard.current.rKey.wasPressedThisFrame)
            {
                playerService.SetPlayerLocomotion(initialPosition.position);
            }
        }
    }
}
