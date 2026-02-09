using UnityEngine;
using Woi.Events;

namespace Woi.HazardSystem
{
    public class CursorStateController : MonoBehaviour
    {
        void OnEnable()
        {
            EventBus.Subscribe<OnHazardResultRequested>(SetCursorVisible);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<OnHazardResultRequested>(SetCursorVisible);
        }

        void SetCursorVisible(OnHazardResultRequested evt)
        {
            SetCursorState(true);
        }

        void SetCursorState(bool state)
        {
            Cursor.visible = state;
            Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}

