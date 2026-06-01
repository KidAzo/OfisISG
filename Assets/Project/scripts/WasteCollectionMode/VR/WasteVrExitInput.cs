using Obvious.Soap;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR: right grip opens/closes the waste exit overlay (same as Tab on PC).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrExitInput : MonoBehaviour
    {
        private const string GripEventPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/preOnGameFinishEvent.asset";

        [SerializeField] private ScriptableEventNoParam gripInputEvent;
        [SerializeField] private WasteResultScreenController resultScreen;
        [SerializeField] private WasteSelectionMenu selectionMenu;

        private void Awake()
        {
            ResolveGripEvent();
            if (resultScreen == null)
                resultScreen = GetComponent<WasteResultScreenController>();
            if (selectionMenu == null)
                selectionMenu = GetComponent<WasteSelectionMenu>();
        }

        private void OnEnable()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            if (gripInputEvent != null)
                gripInputEvent.OnRaised += OnGripInput;
        }

        private void OnDisable()
        {
            if (gripInputEvent != null)
                gripInputEvent.OnRaised -= OnGripInput;
        }

        private void OnGripInput()
        {
            if (resultScreen == null)
                return;

            if (selectionMenu != null && selectionMenu.IsVisible)
                return;

            resultScreen.ToggleExitOverlay();
        }

        private void ResolveGripEvent()
        {
            if (gripInputEvent != null)
                return;

#if UNITY_EDITOR
            gripInputEvent =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEventNoParam>(GripEventPath);
            if (gripInputEvent != null)
                return;
#endif

            ScriptableEventNoParam[] events =
                Resources.FindObjectsOfTypeAll<ScriptableEventNoParam>();
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] != null && events[i].name == "preOnGameFinishEvent")
                {
                    gripInputEvent = events[i];
                    return;
                }
            }
        }
    }
}
