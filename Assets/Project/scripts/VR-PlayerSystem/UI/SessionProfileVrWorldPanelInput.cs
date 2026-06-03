using UnityEngine;
using UnityEngine.UIElements;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler
{
    /// <summary>
    /// Maps XR controller rays onto the session world-space UI Toolkit panel (Unity 6 ScreenToPanelSpaceFunction pattern).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionProfileVrWorldPanelInput : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private float maxRaycastDistance = 2.5f;

        private PanelSettings activePanelSettings;
        private Transform panelHost;
        private Vector2 panelPixelSize = new(520f, 420f);
        private bool hasValidPanelHit;
        private Vector2 lastPanelTexels;

        public void Attach(
            PanelSettings panelSettings,
            UIDocument document,
            Transform host,
            Vector2 worldPanelPixelSize)
        {
            Detach();

            activePanelSettings = panelSettings;
            uiDocument = document;
            panelHost = host;
            panelPixelSize = worldPanelPixelSize;

            if (activePanelSettings != null)
                activePanelSettings.SetScreenToPanelSpaceFunction(ScreenToPanel);
        }

        public void Detach()
        {
            if (activePanelSettings != null)
                activePanelSettings.SetScreenToPanelSpaceFunction(null);

            activePanelSettings = null;
            hasValidPanelHit = false;
        }

        private void LateUpdate()
        {
            if (activePanelSettings == null || panelHost == null)
                return;

            UpdatePanelHitFromGameplayRay();
        }

        private void UpdatePanelHitFromGameplayRay()
        {
            hasValidPanelHit = false;

            if (!WasteCollectionPlatform.ShouldUseVrPresentation())
                return;

            if (!FireVrGameplayInteractionRay.TryGetRay(out Vector3 origin, out Vector3 direction))
                return;

            Ray ray = new(origin, direction);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxRaycastDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
            {
                return;
            }

            if (hit.collider == null || !hit.collider.transform.IsChildOf(panelHost))
                return;

            Vector2 uv = hit.textureCoord;
            uv.y = 1f - uv.y;
            lastPanelTexels = new Vector2(uv.x * panelPixelSize.x, uv.y * panelPixelSize.y);
            hasValidPanelHit = true;
        }

        private Vector2 ScreenToPanel(Vector2 screenPosition)
        {
            if (!hasValidPanelHit)
                return new Vector2(float.NaN, float.NaN);

            return lastPanelTexels;
        }
    }
}
