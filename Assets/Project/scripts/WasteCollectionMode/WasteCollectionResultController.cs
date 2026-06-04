using UnityEngine;
using Woi.Events;
using Woi.Player;
using Woi.SelectionSystem;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Listens for <see cref="WasteCollectedEvent"/>, blocks player input and opens the waste bin selection UI.
    /// </summary>
    public class WasteCollectionResultController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private string playerTag = "Player";

        [Header("Systems")]
        [SerializeField] private SelectionSystemManager selectionSystemManager;
        [SerializeField] private WasteSelectionMenu wasteSelectionMenu;
        [SerializeField] private WasteCollectTracker collectTracker;
        [SerializeField] private WasteExplanationPopup explanationPopup;

        [Header("VR")]
        [SerializeField] private WasteVrLocomotionGate vrLocomotionGate;
        [SerializeField] private WasteWorldUiPresenter worldUiPresenter;

        private readonly PlayerMovementLookFreeze movementLookFreeze = new();
        private bool playerInputFrozen;
        private CursorLockMode savedCursorLockState;
        private bool savedCursorVisible;
        private string pendingWasteName;
        private WasteDefinition pendingDefinition;
        private bool explanationFlowActive;

        private void OnEnable()
        {
            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);

            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            if (explanationPopup == null)
                explanationPopup = FindFirstObjectByType<WasteExplanationPopup>();

            if (vrLocomotionGate == null)
                vrLocomotionGate = FindFirstObjectByType<WasteVrLocomotionGate>();

            if (worldUiPresenter == null)
                worldUiPresenter = GetComponent<WasteWorldUiPresenter>();

            if (wasteSelectionMenu != null)
            {
                wasteSelectionMenu.Dismissed += OnMenuDismissed;
                wasteSelectionMenu.BinSelected += OnBinSelected;
            }
        }

        private void OnDisable()
        {
            EventBus.Deregister<WasteCollectedEvent>(OnWasteCollected);

            if (wasteSelectionMenu != null)
            {
                wasteSelectionMenu.Dismissed -= OnMenuDismissed;
                wasteSelectionMenu.BinSelected -= OnBinSelected;
            }

            RestorePlayerInput();
        }

        private void OnWasteCollected(WasteCollectedEvent evt)
        {
            CancelActiveExplanationFlowIfNeeded();

            pendingWasteName = evt.WasteName;
            FreezePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(false);

            if (wasteSelectionMenu != null)
                wasteSelectionMenu.Show(evt.WasteName);

            RefreshVrPanelForMenu();
        }

        private void OnBinSelected(string binId)
        {
            if (collectTracker == null)
                collectTracker = FindFirstObjectByType<WasteCollectTracker>();

            if (collectTracker == null || string.IsNullOrWhiteSpace(pendingWasteName))
            {
                pendingWasteName = null;
                return;
            }

            collectTracker.RecordClassification(pendingWasteName, binId);

            var classifications = collectTracker.Classifications;
            bool isCorrect = classifications.Count > 0 && classifications[classifications.Count - 1].isCorrect;
            WasteDefinition definition = collectTracker.GetDefinition(pendingWasteName);
            pendingWasteName = null;

            bool hasExplanation = definition != null && definition.HasExplanation;

            // No explanation step: keep the original behaviour (play result sound, resume right away
            // via Dismissed) so VR never drops the world panel waiting on a missing voice line.
            if (!hasExplanation)
            {
                if (ServiceLocator.TryGet(out WasteAudioFeedback resultOnlyAudio))
                    resultOnlyAudio.PlayClassificationResult(isCorrect);
                return;
            }

            // Explanation step: open the popup immediately (status + text) so the VR world panel
            // stays populated, play the correct/wrong sound, then chain the explanation voice.
            pendingDefinition = definition;
            explanationFlowActive = true;

            if (explanationPopup != null)
                explanationPopup.Show(isCorrect, definition.ExplanationText);

            EnableGameplayInputDuringExplanation();

            if (ServiceLocator.TryGet(out WasteAudioFeedback wasteAudio))
                wasteAudio.PlayClassificationResult(isCorrect, OnClassificationSoundFinished);
            else
                OnClassificationSoundFinished();
        }

        private void OnClassificationSoundFinished()
        {
            if (!explanationFlowActive)
                return;

            if (pendingDefinition == null || !pendingDefinition.HasExplanation)
            {
                FinishExplanationFlow();
                return;
            }

            if (ServiceLocator.TryGet(out WasteAudioFeedback wasteAudio))
                wasteAudio.PlayWasteExplanation(pendingDefinition, FinishExplanationFlow);
            else
                FinishExplanationFlow();
        }

        private void FinishExplanationFlow()
        {
            if (explanationPopup != null)
                explanationPopup.Hide();

            pendingDefinition = null;
            explanationFlowActive = false;
            ResumeGameplay();
        }

        private void OnMenuDismissed()
        {
            pendingWasteName = null;

            // While the explanation popup is running its own flow, defer the resume until the
            // explanation audio finishes (FinishExplanationFlow handles it).
            if (explanationFlowActive)
                return;

            ResumeGameplay();
        }

        private void ResumeGameplay()
        {
            if (wasteSelectionMenu != null)
                wasteSelectionMenu.Hide();

            RestorePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(true);
        }

        private void EnableGameplayInputDuringExplanation()
        {
            RestorePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(true);
        }

        /// <summary>Stops explanation audio/UI (e.g. player opens exit with grip mid-voiceover).</summary>
        public void InterruptActiveExplanationFlow()
        {
            CancelActiveExplanationFlowIfNeeded();
        }

        private void CancelActiveExplanationFlowIfNeeded()
        {
            if (!explanationFlowActive && (explanationPopup == null || !explanationPopup.IsVisible))
                return;

            if (ServiceLocator.TryGet(out WasteAudioFeedback wasteAudio))
                wasteAudio.StopActiveFeedback();

            explanationFlowActive = false;
            pendingDefinition = null;

            if (explanationPopup != null)
                explanationPopup.Hide();
        }

        private void RefreshVrPanelForMenu()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            if (worldUiPresenter == null)
                worldUiPresenter = GetComponent<WasteWorldUiPresenter>();

            worldUiPresenter?.NotifyContentLayoutChanged(8);
        }

        private void FreezePlayerInput()
        {
            if (playerInputFrozen)
                return;

            if (WasteCollectionPlatform.IsVR)
            {
                playerInputFrozen = true;
                return;
            }

            Transform root = ResolvePlayerRoot();
            movementLookFreeze.Freeze(root);

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(false);

            savedCursorLockState = UnityEngine.Cursor.lockState;
            savedCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            playerInputFrozen = true;
        }

        private void RestorePlayerInput()
        {
            if (!playerInputFrozen)
                return;

            if (WasteCollectionPlatform.IsVR)
            {
                playerInputFrozen = false;
                return;
            }

            movementLookFreeze.Restore();

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(true);

            if (ServiceLocator.TryGet(out Woi.InputSystem.InputManager inputManager))
                inputManager.EnsurePcGameplayInputEnabled();

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            playerInputFrozen = false;
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
                return playerRoot;

            if (ServiceLocator.TryGet(out IPlayerService playerService))
            {
                Transform serviceRoot = playerService.GetPlayerTransform();
                if (serviceRoot != null)
                    return serviceRoot;
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                    return taggedPlayer.transform;
            }

            return null;
        }
    }
}
