using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.ApplicationController
{
    public class ApplicationManager : MonoBehaviour
    {
        void Update()
        {
            if(Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Application.Quit();
            }
        }
    }
}
