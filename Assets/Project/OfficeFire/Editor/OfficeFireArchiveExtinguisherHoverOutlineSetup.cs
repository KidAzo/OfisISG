using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.Game;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires extinguisher hover outline the same way as <see cref="Alarm"/> — via
    /// <see cref="PCHoverInteractor"/> + <see cref="HoverableOutline"/> + non-trigger collider.
    /// </summary>
    public static class OfficeFireArchiveExtinguisherHoverOutlineSetup
    {
        private const string MenuPath = "Woi/Office Fire/Archive/Wire Extinguisher Hover Outline";

        [MenuItem(MenuPath, false, 26)]
        private static void WireExtinguisherHoverOutlineInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Active scene is not valid or not loaded.");
                return;
            }

            ExtinguisherPickupItem[] items = Object.FindObjectsByType<ExtinguisherPickupItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (items.Length == 0)
            {
                Debug.LogWarning("[Office Fire Scene] No ExtinguisherPickupItem found in active scene.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire Extinguisher Hover Outline");
            int undoGroup = Undo.GetCurrentGroup();

            int wired = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (TryWireItem(items[i]))
                {
                    wired++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[Office Fire Scene] Wired hover outline on {wired}/{items.Length} extinguisher(s) in '{scene.path}'.");
        }

        private static bool TryWireItem(ExtinguisherPickupItem item)
        {
            if (item == null)
            {
                return false;
            }

            GameObject root = item.gameObject;

            HoverOutline[] legacyHoverOutlines = root.GetComponentsInChildren<HoverOutline>(true);
            for (int i = 0; i < legacyHoverOutlines.Length; i++)
            {
                HoverOutline legacy = legacyHoverOutlines[i];
                if (legacy == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(legacy);
            }

            Outline outline = root.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(root);
            }

            Undo.RecordObject(outline, "Configure extinguisher outline");
            outline.OutlineColor = new Color(1f, 0.92f, 0f, 1f);
            outline.OutlineWidth = 2f;
            outline.enabled = false;

            HoverableOutline hoverable = root.GetComponent<HoverableOutline>();
            if (hoverable == null)
            {
                hoverable = Undo.AddComponent<HoverableOutline>(root);
            }

            SerializedObject hoverableSo = new SerializedObject(hoverable);
            SerializedProperty outlineProp = hoverableSo.FindProperty("outline");
            SerializedProperty useWidthProp = hoverableSo.FindProperty("useOutlineWidth");
            SerializedProperty widthProp = hoverableSo.FindProperty("hoverOutlineWidth");

            if (outlineProp != null)
            {
                outlineProp.objectReferenceValue = outline;
            }

            if (useWidthProp != null)
            {
                useWidthProp.boolValue = true;
            }

            if (widthProp != null)
            {
                widthProp.floatValue = 5f;
            }

            hoverableSo.ApplyModifiedPropertiesWithoutUndo();

            EnsurePcHoverCollider(root);
            SetLayerRecursive(root.transform, LayerMask.NameToLayer("Estinguisher"));
            return true;
        }

        private static void EnsurePcHoverCollider(GameObject root)
        {
            BoxCollider[] colliders = root.GetComponents<BoxCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider != null && !collider.isTrigger && collider.enabled)
                {
                    return;
                }
            }

            BoxCollider trigger = root.GetComponent<BoxCollider>();
            BoxCollider hoverCollider = Undo.AddComponent<BoxCollider>(root);
            hoverCollider.isTrigger = false;
            hoverCollider.enabled = true;

            if (trigger != null)
            {
                hoverCollider.center = trigger.center;
                hoverCollider.size = trigger.size;
                return;
            }

            hoverCollider.center = new Vector3(-0.008752391f, 0.25259522f, 0.00008433312f);
            hoverCollider.size = new Vector3(0.2305347f, 0.55517286f, 0.18639208f);
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null || layer < 0)
            {
                return;
            }

            Undo.RecordObject(root.gameObject, "Set extinguisher layer");
            root.gameObject.layer = layer;

            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursive(root.GetChild(i), layer);
            }
        }
    }
}


