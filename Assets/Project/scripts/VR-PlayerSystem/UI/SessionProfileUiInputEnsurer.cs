using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Woi.WasteCollectionMode;

namespace Woi.DataHandler
{
    /// <summary>
    /// Ensures EventSystem modules can send pointer events to the session profile UI Toolkit panel.
    /// </summary>
    public static class SessionProfileUiInputEnsurer
    {
        private static EventSystem s_createdEventSystem;
        private static GameObject s_createdXrUiToolkitManager;
        private static object s_savedPanelInputRedirection;
        private static bool s_savedPanelInputRedirectionValid;
        private static EventSystem s_configuredEventSystem;

        public static void EnsureForSessionOverlay()
        {
            EventSystem eventSystem = EventSystem.current
                ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem (Session UI)");
                eventSystem = go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                s_createdEventSystem = eventSystem;
            }

            s_configuredEventSystem = eventSystem;
            SaveAndConfigurePanelInput(eventSystem);

            if (WasteCollectionPlatform.ShouldUseVrPresentation())
            {
                EnsureXrUiToolkitManager();
                EnsureXrUiInputModule(eventSystem);
            }
            else
            {
                EnsurePcUiInputModule(eventSystem);
            }
        }

        public static void RestoreAfterSessionOverlay()
        {
            EventSystem eventSystem = s_configuredEventSystem
                ?? EventSystem.current
                ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (WasteCollectionPlatform.ShouldUseVrPresentation())
                RestoreVrGameplayUiInput(eventSystem);
            else
                RestorePcGameplayUiInput(eventSystem);

            s_configuredEventSystem = null;
            s_savedPanelInputRedirectionValid = false;
            s_createdXrUiToolkitManager = null;
            s_createdEventSystem = null;
        }

        private static void RestoreVrGameplayUiInput(EventSystem eventSystem)
        {
            EnsureXrUiToolkitManager();
            ConfigurePanelInputForVrNearFar(eventSystem);
            EnsureXrUiInputModule(eventSystem);
            DisableNonXrInputModules(eventSystem);
            RestoreNearFarInteractorsForGameplayUi();
        }

        private static void RestorePcGameplayUiInput(EventSystem eventSystem)
        {
            if (s_savedPanelInputRedirectionValid && eventSystem != null)
                RestoreSavedPanelInputRedirection(eventSystem);
            else
                ForceGameplayPanelInputRedirection(eventSystem);

            RestoreInputModules(eventSystem);
        }

        private static void EnsurePcUiInputModule(EventSystem eventSystem)
        {
            InputSystemUIInputModule pcModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (pcModule == null)
                pcModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            pcModule.enabled = true;
        }

        private static void EnsureXrUiToolkitManager()
        {
            Type managerType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIToolkitManager, Unity.XR.Interaction.Toolkit");

            if (managerType == null)
                return;

            if (UnityEngine.Object.FindObjectsByType(managerType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
                return;

            s_createdXrUiToolkitManager = new GameObject("XRUIToolkitManager (Session UI)");
            s_createdXrUiToolkitManager.AddComponent(managerType);
        }

        private static void EnsureXrUiInputModule(EventSystem eventSystem)
        {
            Type xrModuleType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");

            if (xrModuleType == null)
            {
                EnsurePcUiInputModule(eventSystem);
                return;
            }

            Component xrModule = eventSystem.GetComponent(xrModuleType);
            if (xrModule == null)
                xrModule = eventSystem.gameObject.AddComponent(xrModuleType);

            SetBehaviourEnabled(xrModule, true);
        }

        private static void SaveAndConfigurePanelInput(EventSystem eventSystem)
        {
            Type picType = Type.GetType(
                "UnityEngine.UIElements.PanelInputConfiguration, UnityEngine.UIElementsModule");

            if (picType == null || eventSystem == null)
                return;

            Component pic = eventSystem.GetComponent(picType);
            if (pic == null)
                pic = eventSystem.gameObject.AddComponent(picType);

            SavePanelInputRedirection(picType, pic);
            ConfigurePanelInputForVrNearFar(eventSystem);
        }

        private static void ConfigurePanelInputForVrNearFar(EventSystem eventSystem)
        {
            if (eventSystem == null)
                return;

            Type picType = Type.GetType(
                "UnityEngine.UIElements.PanelInputConfiguration, UnityEngine.UIElementsModule");

            if (picType == null)
                return;

            Component pic = eventSystem.GetComponent(picType);
            if (pic == null)
                pic = eventSystem.gameObject.AddComponent(picType);

            foreach (string enumName in new[] { "Never", "NoInputRedirection", "NoInput", "None" })
            {
                TrySetPanelInputRedirection(picType, pic, enumName);
            }
        }

        private static void RestoreSavedPanelInputRedirection(EventSystem eventSystem)
        {
            if (eventSystem == null || !s_savedPanelInputRedirectionValid)
                return;

            Type picType = Type.GetType(
                "UnityEngine.UIElements.PanelInputConfiguration, UnityEngine.UIElementsModule");

            if (picType == null)
                return;

            Component pic = eventSystem.GetComponent(picType);
            if (pic == null)
                return;

            PropertyInfo property = picType.GetProperty(
                "panelInputRedirection",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || !property.CanWrite)
                return;

            try
            {
                property.SetValue(pic, s_savedPanelInputRedirection);
            }
            catch
            {
                ForceGameplayPanelInputRedirection(eventSystem);
            }
        }

        private static void ForceGameplayPanelInputRedirection(EventSystem eventSystem)
        {
            if (eventSystem == null)
                return;

            Type picType = Type.GetType(
                "UnityEngine.UIElements.PanelInputConfiguration, UnityEngine.UIElementsModule");

            if (picType == null)
                return;

            Component pic = eventSystem.GetComponent(picType);
            if (pic == null)
                return;

            TrySetPanelInputRedirection(picType, pic, "All");
            TrySetPanelInputRedirection(picType, pic, "Automatic");
        }

        private static void DisableNonXrInputModules(EventSystem eventSystem)
        {
            if (eventSystem == null)
                return;

            Type xrModuleType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");

            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (xrModuleType != null && module != null && xrModuleType.IsInstanceOfType(module))
                    continue;

                if (module != null)
                    module.enabled = false;
            }
        }

        private static void RestoreNearFarInteractorsForGameplayUi()
        {
            Type nearFarType = Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor, Unity.XR.Interaction.Toolkit");

            if (nearFarType == null)
                return;

            UnityEngine.Object[] interactors = UnityEngine.Object.FindObjectsByType(
                nearFarType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < interactors.Length; i++)
            {
                if (interactors[i] is not Behaviour interactor || !interactor.isActiveAndEnabled)
                    continue;

                TrySetEnableUiInteraction(interactor);
                TryMergeRaycastMask(interactor, Physics.DefaultRaycastLayers);
            }
        }

        private static void TrySetEnableUiInteraction(object interactor)
        {
            if (interactor == null)
                return;

            const string propName = "enableUIInteraction";
            for (Type type = interactor.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(
                    propName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
                {
                    try
                    {
                        property.SetValue(interactor, true);
                        return;
                    }
                    catch
                    {
                        return;
                    }
                }

                FieldInfo field = type.GetField(
                    "m_EnableUIInteraction",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null && field.FieldType == typeof(bool))
                {
                    try
                    {
                        field.SetValue(interactor, true);
                        return;
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        private static void TryMergeRaycastMask(object interactor, LayerMask extra)
        {
            if (interactor == null)
                return;

            Type type = interactor.GetType();
            foreach (string propName in new[] { "raycastMask", "m_RaycastMask" })
            {
                PropertyInfo property = type.GetProperty(
                    propName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property == null || !property.CanRead || !property.CanWrite || property.PropertyType != typeof(LayerMask))
                    continue;

                try
                {
                    var current = (LayerMask)property.GetValue(interactor);
                    int merged = current.value | extra.value;
                    property.SetValue(interactor, (LayerMask)merged);
                    return;
                }
                catch
                {
                    // Try next binding.
                }
            }

            FieldInfo field = type.GetField("m_RaycastMask", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(LayerMask))
                return;

            try
            {
                var current = (LayerMask)field.GetValue(interactor);
                int merged = current.value | extra.value;
                field.SetValue(interactor, (LayerMask)merged);
            }
            catch
            {
                // Best effort.
            }
        }

        private static void RestoreInputModules(EventSystem eventSystem)
        {
            if (eventSystem == null)
                return;

            InputSystemUIInputModule pcModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (pcModule != null)
                pcModule.enabled = true;
        }

        private static void SavePanelInputRedirection(Type picType, object pic)
        {
            PropertyInfo property = picType.GetProperty(
                "panelInputRedirection",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || !property.CanRead)
                return;

            s_savedPanelInputRedirection = property.GetValue(pic);
            s_savedPanelInputRedirectionValid = s_savedPanelInputRedirection != null;
        }

        private static void TrySetPanelInputRedirection(Type hostType, object instance, string enumValueName)
        {
            PropertyInfo property = hostType.GetProperty(
                "panelInputRedirection",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            try
            {
                object value = Enum.Parse(property.PropertyType, enumValueName, ignoreCase: true);
                property.SetValue(instance, value);
            }
            catch
            {
                // Try next name in caller loop.
            }
        }

        private static void SetBehaviourEnabled(Component component, bool enabled)
        {
            if (component is Behaviour behaviour)
                behaviour.enabled = enabled;
        }
    }
}
