using System.Collections.Generic;
using System.Reflection;
using FireExtinguisher.Core;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Woi.Equipment;
using Woi.InputSystem;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires the kitchen "Carafe" pickup to behave exactly like the fire blanket
    /// (E to equip, G near fire to use, hover outline), with the single difference that
    /// using it on a fire makes the fire grow. Also swaps Carafe → CarafeAndVfx on use.
    /// </summary>
    public static class OfficeFireCarafeSetup
    {
        private const string GameplayInputContextAssetPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/PC-GameplayContext.asset";

        private const string MenuPath = "Tools/Woi/Office Fire/Scene/Wire Carafe";
        private const string CarafeObjectName = "Carafe";
        private const string CarafeVfxObjectName = "CarafeAndVfx";

        [MenuItem(MenuPath, false, 26)]
        private static void WireCarafeActiveScene()
        {
            WireCarafeInScene(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuPath, true, 26)]
        private static bool WireCarafeActiveSceneValidate()
        {
            return !Application.isPlaying;
        }

        public static void WireCarafeInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Office Fire Scene] Scene is not valid or not loaded: " + scene.path);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Office Fire: Wire Carafe");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject carafe = FindInScene(scene, CarafeObjectName);
            GameObject carafeVfx = FindInScene(scene, CarafeVfxObjectName);

            if (carafe == null)
            {
                Debug.LogError($"[Office Fire Scene] '{CarafeObjectName}' object not found in scene.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            CarafePickupItem item = WireCarafeItem(carafe);

            if (carafeVfx != null && carafeVfx.activeSelf)
            {
                Undo.RecordObject(carafeVfx, "Disable CarafeAndVfx");
                carafeVfx.SetActive(false);
                EditorUtility.SetDirty(carafeVfx);
            }

            int playerCount = WirePlayerCarafeEquipment(carafe, carafeVfx);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[Office Fire Scene] Carafe wiring complete ({scene.path}). " +
                $"Item wired={item != null}, players wired={playerCount}, vfx found={carafeVfx != null}.");
        }

        private static CarafePickupItem WireCarafeItem(GameObject carafe)
        {
            RemoveLegacyHoverComponents(carafe);

            Outline outline = carafe.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(carafe);
            }

            Undo.RecordObject(outline, "Configure carafe outline");
            outline.OutlineColor = new Color(1f, 0.92f, 0f, 1f);
            outline.OutlineWidth = 2f;
            outline.enabled = false;

            CarafePickupItem item = carafe.GetComponent<CarafePickupItem>();
            if (item == null)
            {
                item = Undo.AddComponent<CarafePickupItem>(carafe);
            }

            EnsurePickupCollider(carafe);
            Transform dropAnchor = EnsureDropAnchor(carafe);

            SerializedObject so = new SerializedObject(item);
            SetObjectReference(so, "outline", outline);
            SetObjectReference(so, "dropAnchor", dropAnchor);
            SetBool(so, "useOutlineWidth", true);
            SetFloat(so, "hoverOutlineWidth", 5f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return item;
        }

        private static int WirePlayerCarafeEquipment(GameObject carafe, GameObject carafeVfx)
        {
            PlayerFireBlanketEquipment[] blanketHosts = Object.FindObjectsByType<PlayerFireBlanketEquipment>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            List<GameObject> hosts = new List<GameObject>();
            for (int i = 0; i < blanketHosts.Length; i++)
            {
                if (blanketHosts[i] != null && !hosts.Contains(blanketHosts[i].gameObject))
                {
                    hosts.Add(blanketHosts[i].gameObject);
                }
            }

            if (hosts.Count == 0)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                {
                    hosts.Add(taggedPlayer);
                }
            }

            FireSource fireSource = ResolveClosestFireSource(carafe.transform.position);

            int wired = 0;
            for (int i = 0; i < hosts.Count; i++)
            {
                if (WireOnePlayer(hosts[i], fireSource, carafeVfx))
                {
                    wired++;
                }
            }

            return wired;
        }

        private static bool WireOnePlayer(GameObject player, FireSource fireSource, GameObject carafeVfx)
        {
            if (player == null)
            {
                return false;
            }

            PlayerFireBlanketEquipment blanketEquipment = player.GetComponent<PlayerFireBlanketEquipment>();
            PlayerExtinguisherEquipment extinguisherEquipment = player.GetComponent<PlayerExtinguisherEquipment>();

            PlayerCarafeEquipment equipment = player.GetComponent<PlayerCarafeEquipment>();
            if (equipment == null)
            {
                equipment = Undo.AddComponent<PlayerCarafeEquipment>(player);
            }

            CarafeUseController useController = player.GetComponent<CarafeUseController>();
            if (useController == null)
            {
                useController = Undo.AddComponent<CarafeUseController>(player);
            }

            Transform equipAnchor = blanketEquipment != null ? blanketEquipment.EquipAnchor : null;
            if (equipAnchor == null && extinguisherEquipment != null)
            {
                equipAnchor = extinguisherEquipment.EquipAnchor;
            }

            GameplayInputContext inputContext = extinguisherEquipment != null ? extinguisherEquipment.InputContext : null;
            if (inputContext == null)
            {
                inputContext = AssetDatabase.LoadAssetAtPath<GameplayInputContext>(GameplayInputContextAssetPath);
            }

            Camera playerCamera = extinguisherEquipment != null ? extinguisherEquipment.PlayerCamera : null;

            SerializedObject equipmentSo = new SerializedObject(equipment);
            SetObjectReference(equipmentSo, "equipAnchor", equipAnchor);
            SetObjectReference(equipmentSo, "inputContext", inputContext);
            if (playerCamera != null)
            {
                SetObjectReference(equipmentSo, "playerCamera", playerCamera);
            }
            SetInt(equipmentSo, "pickupLayerMask", Physics.AllLayers);
            equipmentSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject useSo = new SerializedObject(useController);
            SetObjectReference(useSo, "carafeEquipment", equipment);
            SetObjectReference(useSo, "fireSource", fireSource);
            SetObjectReference(useSo, "distanceReference", player.transform);
            SetFloat(useSo, "fireZoneProbeRadius", 3f);
            SetFloat(useSo, "vfxResetDelaySeconds", 4f);
            SetString(useSo, "useInstructionText", "Approach the fire and press G to pour the carafe");
            SetString(useSo, "useInstructionTextTurkish", "Yangına yaklaş ve G ile dök");
            useSo.ApplyModifiedPropertiesWithoutUndo();

            WireBoolEvent(useController, "onCarafeUsedOnFire", carafeVfx, true);
            WireBoolEvent(useController, "onCarafeReset", carafeVfx, false);
            return true;
        }

        private static void WireBoolEvent(
            CarafeUseController useController,
            string eventFieldName,
            GameObject target,
            bool activeValue)
        {
            if (useController == null || target == null)
            {
                return;
            }

            FieldInfo eventField = typeof(CarafeUseController).GetField(
                eventFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (eventField?.GetValue(useController) is not UnityEvent unityEvent)
            {
                return;
            }

            // Avoid duplicate listeners on re-run.
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (ReferenceEquals(unityEvent.GetPersistentTarget(i), target))
                {
                    return;
                }
            }

            UnityEventTools.AddBoolPersistentListener(unityEvent, target.SetActive, activeValue);
            EditorUtility.SetDirty(useController);
        }

        private static FireSource ResolveClosestFireSource(Vector3 position)
        {
            FireSource[] sources = Object.FindObjectsByType<FireSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            FireSource best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < sources.Length; i++)
            {
                FireSource source = sources[i];
                if (source == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, source.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = source;
                }
            }

            return best;
        }

        private static Transform EnsureDropAnchor(GameObject carafe)
        {
            Transform parent = carafe.transform.parent;
            string anchorName = "CarafeDropAnchor";

            if (parent != null)
            {
                Transform existing = parent.Find(anchorName);
                if (existing != null)
                {
                    return existing;
                }
            }

            GameObject anchor = new GameObject(anchorName);
            Undo.RegisterCreatedObjectUndo(anchor, "Create carafe drop anchor");
            anchor.transform.SetParent(parent, worldPositionStays: false);
            anchor.transform.SetPositionAndRotation(carafe.transform.position, carafe.transform.rotation);
            return anchor.transform;
        }

        private static void EnsurePickupCollider(GameObject root)
        {
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(root);
            }

            Undo.RecordObject(box, "Configure carafe collider");
            box.isTrigger = false;
            box.enabled = true;

            if (TryFitBoxColliderToRenderers(root.transform, box))
            {
                return;
            }

            box.center = Vector3.zero;
            box.size = new Vector3(0.3f, 0.3f, 0.3f);
        }

        private static bool TryFitBoxColliderToRenderers(Transform root, BoxCollider box)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector3 localCenter = root.InverseTransformPoint(bounds.center);
            Vector3 localSize = root.InverseTransformVector(bounds.size);
            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            if (localSize.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            box.center = localCenter;
            box.size = localSize;
            return true;
        }

        private static void RemoveLegacyHoverComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "HoverableOutline" || typeName == "HoverOutline" || typeName == "SelectableInstructionPrompt")
                {
                    Undo.DestroyObjectImmediate(behaviour);
                }
            }
        }

        private static GameObject FindInScene(Scene scene, string targetName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j] != null && transforms[j].name == targetName)
                    {
                        return transforms[j].gameObject;
                    }
                }
            }

            return null;
        }

        private static void SetObjectReference(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string propertyName, int value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetString(SerializedObject so, string propertyName, string value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }
    }
}
