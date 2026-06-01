using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR waste UI session: modal panels share one HMD-centered distance, locomotion/teleport off, UI-only input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrUiSessionController : MonoBehaviour
    {
        [Header("UI panels")]
        [SerializeField] private WasteSelectionMenu selectionMenu;
        [SerializeField] private WasteResultScreenController resultScreen;

        [Header("Presentation")]
        [SerializeField] private WasteWorldUiPresenter worldUiPresenter;
        [SerializeField] private float uiDistanceInFrontOfHmd = 1.35f;

        [Header("Input / locomotion")]
        [SerializeField] private WasteVrLocomotionGate locomotionGate;
        [SerializeField] private SelectionSystemManager selectionSystemManager;
        [SerializeField] private SelectionVrInteractionRay selectionRay;

        private bool modalUiOpen;
        private bool sessionApplied;

        private void Awake()
        {
            if (selectionMenu == null)
                selectionMenu = GetComponent<WasteSelectionMenu>();

            if (resultScreen == null)
                resultScreen = GetComponent<WasteResultScreenController>();

            if (worldUiPresenter == null)
                worldUiPresenter = GetComponent<WasteWorldUiPresenter>();

            if (locomotionGate == null)
                locomotionGate = GetComponent<WasteVrLocomotionGate>();

            if (selectionSystemManager == null)
                selectionSystemManager = FindFirstObjectByType<SelectionSystemManager>();

            if (selectionRay == null)
                selectionRay = FindFirstObjectByType<SelectionVrInteractionRay>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            bool shouldBeOpen = IsAnyModalUiVisible();
            if (shouldBeOpen == modalUiOpen && sessionApplied)
                return;

            modalUiOpen = shouldBeOpen;
            ApplySession(modalUiOpen);
        }

        private void OnDisable()
        {
            if (!sessionApplied)
                return;

            sessionApplied = false;
            modalUiOpen = false;

            if (worldUiPresenter != null)
                worldUiPresenter.SetFollowActive(false);

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(true);

            if (selectionRay != null)
                selectionRay.SetGameplayRayEnabled(true);

            if (locomotionGate != null)
                locomotionGate.SetLocomotionEnabled(true);
        }

        private bool IsAnyModalUiVisible()
        {
            if (selectionMenu != null && selectionMenu.IsVisible)
                return true;

            if (resultScreen != null && resultScreen.IsVisible)
                return true;

            return false;
        }

        private void ApplySession(bool open)
        {
            sessionApplied = open;

            if (worldUiPresenter != null)
            {
                worldUiPresenter.SetUiDistance(uiDistanceInFrontOfHmd);
                worldUiPresenter.ApplyLayoutFromInspector();
                worldUiPresenter.SetFollowActive(open);
            }

            if (locomotionGate != null)
                locomotionGate.SetLocomotionEnabled(!open);

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(!open);

            if (selectionRay != null)
                selectionRay.SetGameplayRayEnabled(!open);
        }
    }
}
