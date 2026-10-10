#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Woi.Editor.Addressables
{
    /// <summary>
    /// Warns while Office.unity is both an enabled player scene and the HazardHunt_Office address.
    /// Does not remove either entry. Standalone loading tolerates this until one side is dropped.
    /// </summary>
    [InitializeOnLoad]
    static class HazardOfficeSceneOwnershipWarning
    {
        const string OfficeScenePath = "Assets/Project/Scenes/Office.unity";
        const string Address = "HazardHunt_Office";

        const string Message =
            "[HazardOffice] Office.unity is enabled in Build Settings and addressable as HazardHunt_Office. " +
            "Standalone Hazard keeps using SceneManager while that build entry stays enabled, and " +
            "HazardOfficeStandaloneLoader falls back to the HazardHunt_Office address if Unity disables it. " +
            "Do not toggle either side by hand without checking the other. This warning does not remove either entry.";

        static bool _warned;
        static bool _queued;

        static HazardOfficeSceneOwnershipWarning()
        {
            QueueCheck();
            EditorBuildSettings.sceneListChanged += QueueCheck;
            AddressableAssetSettings.OnModificationGlobal += OnAddressablesChanged;
        }

        [MenuItem("Woi/Addressables/Validate Hazard Office Scene Ownership", priority = 44)]
        static void ValidateFromMenu()
        {
            _warned = false;
            Check();
            if (!IsDualUse(out _))
                Debug.Log("[HazardOffice] Office.unity is not currently both an enabled build scene and HazardHunt_Office.");
        }

        static void OnAddressablesChanged(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evt, object eventData)
        {
            QueueCheck();
        }

        static void QueueCheck()
        {
            if (_queued)
                return;

            _queued = true;
            EditorApplication.delayCall += () =>
            {
                _queued = false;
                Check();
            };
        }

        static void Check()
        {
            if (!IsDualUse(out string detail))
            {
                _warned = false;
                return;
            }

            if (_warned)
                return;

            _warned = true;
            Debug.LogWarning(Message + " " + detail);
        }

        static bool IsDualUse(out string detail)
        {
            detail = string.Empty;
            bool enabledInBuild = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path == OfficeScenePath)
                {
                    enabledInBuild = true;
                    break;
                }
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return false;

            string guid = AssetDatabase.AssetPathToGUID(OfficeScenePath);
            AddressableAssetEntry entry = string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid);
            bool addressable = entry != null && entry.address == Address;
            if (!enabledInBuild || !addressable)
                return false;

            detail = "Build Settings enabled=true, address=" + entry.address + ".";
            return true;
        }
    }
}
#endif
