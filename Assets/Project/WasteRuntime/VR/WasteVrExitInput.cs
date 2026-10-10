using Obvious.Soap;
using UnityEngine;
using Woi.InputSystem;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR: right grip opens/closes the waste exit overlay (same as Tab on PC).
    /// Works during Doğru/Yanlış voiceover; waste bin menu still blocks grip until dismissed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrExitInput : MonoBehaviour, ISoapVrGripInputListener
    {
        private const string GripEventPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/preOnGameFinishEvent.asset";

        [SerializeField] private ScriptableEventNoParam gripInputEvent;
        [SerializeField] private WasteResultScreenController resultScreen;
        [SerializeField] private WasteSelectionMenu selectionMenu;
        [SerializeField] private WasteCollectionResultController collectionFlow;

        private void Awake()
        {
            ResolveGripEvent();
            if (resultScreen == null)
                resultScreen = GetComponent<WasteResultScreenController>();
            if (selectionMenu == null)
                selectionMenu = GetComponent<WasteSelectionMenu>();
            if (collectionFlow == null)
                collectionFlow = GetComponent<WasteCollectionResultController>();
        }

        private void OnEnable()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            ResolveGripEvent();
            SubscribeGrip();
        }

        private void OnDisable()
        {
            UnsubscribeGrip();
        }

        public bool IsListeningToDifferentGripEvent(ScriptableEventNoParam liveGripEvent) =>
            gripInputEvent != null
            && liveGripEvent != null
            && !ReferenceEquals(gripInputEvent, liveGripEvent);

        public void RebindGripInputEvent(ScriptableEventNoParam liveGripEvent)
        {
            UnsubscribeGrip();
            gripInputEvent = liveGripEvent;
            if (isActiveAndEnabled && WasteCollectionPlatform.ShouldUseVrPresentation())
                SubscribeGrip();
        }

        private void SubscribeGrip()
        {
            if (gripInputEvent != null)
                gripInputEvent.OnRaised += OnGripInput;
        }

        private void UnsubscribeGrip()
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

            collectionFlow?.InterruptActiveExplanationFlow();

            resultScreen.ToggleExitOverlay();
        }

        private void ResolveGripEvent()
        {
            if (gripInputEvent != null)
                return;

            if (ServiceLocator.TryGet<InputManager>(out InputManager inputManager) && inputManager != null)
            {
                VrInputContext vrContext = inputManager.GetVrInputContext();
                if (vrContext != null && vrContext.PreOnGameplayFinishedEvent != null)
                {
                    gripInputEvent = vrContext.PreOnGameplayFinishedEvent;
                    return;
                }
            }

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
