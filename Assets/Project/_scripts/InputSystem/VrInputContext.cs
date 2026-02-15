using Obvious.Soap;
using UnityEngine;

namespace Woi.InputSystem
{
    [CreateAssetMenu(menuName = "Input System/Contexts/VR Context")]
    public class VrInputContext : InputContext
    {
        [SerializeField] private ScriptableEventNoParam onInteractInput;

        public override void OnEnter()
        {
        if (inputActions == null)
            {
                Debug.LogError("[VrInputContext] InputActions null!");
                return;
            }
            
            inputActions.VR.Enable();
        }

        public override void OnExit()
        {
            if (inputActions == null) return;
            
            inputActions.VR.Disable();
        }
    }
}



