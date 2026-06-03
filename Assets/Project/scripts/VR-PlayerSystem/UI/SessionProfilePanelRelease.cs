using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Woi.DataHandler
{
    /// <summary>
    /// Aggressively releases UI Toolkit world/screen panels so VR physics and UI rays are not blocked after session ends.
    /// </summary>
    public static class SessionProfilePanelRelease
    {
        private static readonly int IgnoreRaycastLayer = 2;

        public static void ApplyNonBlockingLayers(Transform host)
        {
            if (host == null)
                return;

            int layer = IgnoreRaycastLayer;
            SetLayerRecursively(host, layer);
        }

        public static void DisablePickColliders(Transform host)
        {
            if (host == null)
                return;

            Collider[] colliders = host.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                    collider.enabled = false;
            }
        }

        public static void Release(UIDocument document, Transform host)
        {
            if (host != null)
                ApplyNonBlockingLayers(host);

            if (document != null)
            {
                VisualElement root = document.rootVisualElement;
                if (root != null)
                {
                    root.style.display = DisplayStyle.None;
                    root.RemoveFromHierarchy();
                }

                TryDisposePanel(document);
                document.sortingOrder = -1;
                document.panelSettings = null;
                document.enabled = false;
            }

            if (host == null)
                return;

            host.SetPositionAndRotation(new Vector3(0f, -100000f, 0f), Quaternion.identity);
            host.localScale = Vector3.one;

            Collider[] colliders = host.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                    Object.Destroy(collider);
            }
        }

        private static void TryDisposePanel(UIDocument document)
        {
            if (document?.rootVisualElement?.panel == null)
                return;

            object panel = document.rootVisualElement.panel;
            MethodInfo dispose = panel.GetType().GetMethod(
                "Dispose",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            dispose?.Invoke(panel, null);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
