using UnityEngine;
using Woi.SelectionSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// VR waste UI session: only the waste bin menu tracks the HMD. Locomotion/selection are off during menu
    /// and result; they stay on during the Doğru/Yanlış explanation popup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WasteVrUiSessionController : MonoBehaviour
    {
        [Header("UI panels")]
        [SerializeField] private WasteSelectionMenu selectionMenu;
        [SerializeField] private WasteResultScreenController resultScreen;
        [SerializeField] private WasteExplanationPopup explanationPopup;

        [Header("Presentation")]
        [SerializeField] private WasteWorldUiPresenter worldUiPresenter;
        [SerializeField] private float uiDistanceInFrontOfHmd = 1.35f;

        [Header("Input / locomotion")]
        [SerializeField] private WasteVrLocomotionGate locomotionGate;
        [SerializeField] private SelectionSystemManager selectionSystemManager;
        [SerializeField] private SelectionVrInteractionRay selectionRay;

        private bool modalUiOpen;
        private bool gameplayInputBlocked;
        private bool headTrackingActive;
        private bool sessionApplied;
        private int lastModalFingerprint = -1;

        private void Awake()
        {
            if (selectionMenu == null)
                selectionMenu = GetComponent<WasteSelectionMenu>();

            if (resultScreen == null)
                resultScreen = GetComponent<WasteResultScreenController>();

            if (explanationPopup == null)
                explanationPopup = GetComponent<WasteExplanationPopup>();

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
            bool shouldBlockInput = ShouldBlockGameplayInput();
            bool shouldTrackHead = ShouldTrackHead();
            if (shouldBeOpen == modalUiOpen && sessionApplied && shouldBlockInput == gameplayInputBlocked
                && shouldTrackHead == headTrackingActive)
                return;

            modalUiOpen = shouldBeOpen;
            gameplayInputBlocked = shouldBlockInput;
            headTrackingActive = shouldTrackHead;
            ApplySession(modalUiOpen, gameplayInputBlocked, headTrackingActive);

            int fingerprint = ComputeModalFingerprint();
            if (modalUiOpen && fingerprint != lastModalFingerprint)
            {
                lastModalFingerprint = fingerprint;
                int settleFrames = 4;
                if (resultScreen != null && resultScreen.IsResultVisible)
                    settleFrames = 12;
                else if (explanationPopup != null && explanationPopup.IsVisible)
                    settleFrames = 8;

                worldUiPresenter?.NotifyContentLayoutChanged(settleFrames);
            }
            else if (!modalUiOpen)
            {
                lastModalFingerprint = -1;
            }
        }

        private void OnDisable()
        {
            if (!sessionApplied)
                return;

            sessionApplied = false;
            modalUiOpen = false;
            gameplayInputBlocked = false;
            headTrackingActive = false;

            if (worldUiPresenter != null)
                worldUiPresenter.SetFollowActive(false);

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(true);

            if (selectionRay != null)
                selectionRay.RefreshGameplayRay();

            if (locomotionGate != null)
                locomotionGate.SetLocomotionEnabled(true);
        }

        private bool IsAnyModalUiVisible()
        {
            if (selectionMenu != null && selectionMenu.IsVisible)
                return true;

            if (resultScreen != null && resultScreen.IsVisible)
                return true;

            if (explanationPopup != null && explanationPopup.IsVisible)
                return true;

            return false;
        }

        private bool ShouldBlockGameplayInput()
        {
            if (selectionMenu != null && selectionMenu.IsVisible)
                return true;

            if (resultScreen != null && resultScreen.IsVisible)
                return true;

            return false;
        }

        /// <summary>
        /// Only the waste bin menu follows the HMD. Doğru/Yanlış, result and exit stay fixed for stable XR hits.
        /// </summary>
        private bool ShouldTrackHead()
        {
            return selectionMenu != null && selectionMenu.IsVisible;
        }

        private int ComputeModalFingerprint()
        {
            int fingerprint = 0;
            if (selectionMenu != null && selectionMenu.IsVisible)
                fingerprint |= 1 << 0;
            if (resultScreen != null && resultScreen.IsVisible)
                fingerprint |= 1 << 1;
            if (resultScreen != null && resultScreen.IsExitVisible)
                fingerprint |= 1 << 2;
            if (resultScreen != null && resultScreen.IsResultVisible)
                fingerprint |= 1 << 3;
            if (explanationPopup != null && explanationPopup.IsVisible)
                fingerprint |= 1 << 4;
            return fingerprint;
        }

        private void ApplySession(bool worldUiOpen, bool blockGameplayInput, bool trackHead)
        {
            sessionApplied = worldUiOpen;

            if (worldUiPresenter != null)
            {
                worldUiPresenter.SetUiDistance(uiDistanceInFrontOfHmd);
                worldUiPresenter.ApplyLayoutFromInspector();
                worldUiPresenter.SetFollowActive(worldUiOpen, trackHead);
            }

            if (locomotionGate != null)
                locomotionGate.SetLocomotionEnabled(!blockGameplayInput);

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(!blockGameplayInput);

            if (selectionRay != null)
                selectionRay.RefreshGameplayRay();
        }
    }
}
