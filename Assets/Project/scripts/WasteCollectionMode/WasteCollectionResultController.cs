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

        private readonly PlayerMovementLookFreeze movementLookFreeze = new();
        private bool playerInputFrozen;
        private CursorLockMode savedCursorLockState;
        private bool savedCursorVisible;

        private void OnEnable()
        {
            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);

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
            FreezePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(false);

            if (wasteSelectionMenu != null)
                wasteSelectionMenu.Show(evt.WasteName);
        }

        private void OnBinSelected(string binId)
        {
            Debug.Log($"[WasteCollectionResultController] Bin selected: {binId}");
        }

        private void OnMenuDismissed()
        {
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

        private void FreezePlayerInput()
        {
            if (playerInputFrozen)
                return;

            Transform root = ResolvePlayerRoot();
            movementLookFreeze.Freeze(root);

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(false);

            savedCursorLockState = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            playerInputFrozen = true;
        }

        private void RestorePlayerInput()
        {
            if (!playerInputFrozen)
                return;

            movementLookFreeze.Restore();

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(true);

            Cursor.lockState = savedCursorLockState;
            Cursor.visible = savedCursorVisible;
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
