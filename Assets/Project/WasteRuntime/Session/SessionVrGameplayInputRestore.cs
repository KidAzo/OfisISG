using UnityEngine;
using Woi.SelectionSystem;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler
{
    /// <summary>
    /// Restores VR gameplay rays and event routing after the session profile panel closes.
    /// </summary>
    public static class SessionVrGameplayInputRestore
    {
        public static void RestoreIfNeeded()
        {
            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            SessionProfileUiInputEnsurer.RestoreAfterSessionOverlay();
            RestoreSelectionRays();
            RestoreSelectionManagers();
        }

        private static void RestoreSelectionRays()
        {
            SelectionVrInteractionRay[] rays = Object.FindObjectsByType<SelectionVrInteractionRay>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < rays.Length; i++)
            {
                SelectionVrInteractionRay ray = rays[i];
                if (ray == null)
                    continue;

                ray.RefreshGameplayRay();
            }
        }

        private static void RestoreSelectionManagers()
        {
            SelectionSystemManager[] managers = Object.FindObjectsByType<SelectionSystemManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < managers.Length; i++)
            {
                SelectionSystemManager manager = managers[i];
                if (manager != null)
                    manager.SetSelectionInputEnabled(true);
            }
        }
    }
}