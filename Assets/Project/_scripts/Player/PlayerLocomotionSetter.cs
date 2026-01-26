using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.Player
{
    public class PlayerLocomotionSetter : MonoBehaviour
    {
        [Inject] IPlayerService playerService;
        [SerializeField] Transform initialPosition;

        void Start()
        {
            HandlePlayerRegistered();
            playerService.OnPlayerRegistered += HandlePlayerRegistered;
        }

        void OnDestroy()
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
