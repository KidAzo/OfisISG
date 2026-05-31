using UnityEngine;
using Woi.Events;
using Woi.Player;
using Woi.SelectionSystem;
using WOI.Modules.SDK;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Listens for <see cref="WasteCollectedEvent"/>, blocks player look/movement and opens the result UI.
    /// </summary>
    public class WasteCollectionResultController : MonoBehaviour
    {
        [SerializeField] private SelectionSystemManager selectionSystemManager;
        [SerializeField] private GameObject resultPanel;

        private bool playerInputFrozen;
        private CursorLockMode savedCursorLockState;
        private bool savedCursorVisible;

        private void Awake()
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Register<WasteCollectedEvent>(OnWasteCollected);
        }

        private void OnDisable()
        {
            EventBus.Deregister<WasteCollectedEvent>(OnWasteCollected);
            RestorePlayerInput();
        }

        private void OnWasteCollected(WasteCollectedEvent evt)
        {
            FreezePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(false);

            if (resultPanel != null)
                resultPanel.SetActive(true);
        }

        /// <summary>
        /// Call from UI close button when the result screen is dismissed.
        /// </summary>
        public void CloseResultPanel()
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);

            RestorePlayerInput();

            if (selectionSystemManager != null)
                selectionSystemManager.SetSelectionInputEnabled(true);
        }

        private void FreezePlayerInput()
        {
            if (playerInputFrozen)
                return;

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

            if (ServiceLocator.TryGet(out IPlayerService playerService))
                playerService.SetPlayerInputEnabled(true);

            Cursor.lockState = savedCursorLockState;
            Cursor.visible = savedCursorVisible;
            playerInputFrozen = false;
        }
    }
}
