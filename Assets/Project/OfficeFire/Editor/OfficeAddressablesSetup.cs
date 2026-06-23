#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace Woi.Editor.Addressables
{
    /// <summary>
    /// Configures Addressables for the office-safety Hub module (remote catalog + Office_Safety group).
    /// Mirrors the Fire_Train setup in this project.
    /// </summary>
    public static class OfficeAddressablesSetup
    {
        const string OfficeGroupName = "Office_Safety";
        const string DownloadLabel = "module-office-fire-full";
        const string OfficeProfileName = "Office";
        const string RemoteBuildPath = "ServerData/Office/[BuildTarget]";
        const string ModuleVersion = "0.3.0";
        const string RemoteLoadPath = "https://storage.googleapis.com/digitech-hub-388ab.firebasestorage.app/modules/office/fire/pc/" + ModuleVersion;

        static readonly (string guid, string address)[] RequiredSceneEntries =
        {
            ("6f42e3fa241219d4994017267ad51a18", "Office_Boot"),
            ("7638b2d478e05b9419ed7e07ebfd9617", "OfficeFireModule_Login"),
            ("2a034a5d92c28e444a1e19d5ef67d9f3", "FireModule_Office"),
            ("86ac583fd775cc949aa267c369d36dd2", "FireStairsOfis"),
            ("6e53403b119b63d4ab5ab1a8b6817a14", "FireStairsOfis 1"),
            ("bff5ae25ecb1c5545b27ca6f890bf48c", "OutDoor"),
        };

        [MenuItem("Woi/Addressables/Configure Office Safety Module", priority = 20)]
        public static void ConfigureOfficeSafetyModule()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Addressables", "AddressableAssetSettings not found.", "OK");
                return;
            }

            EnsureRemoteCatalogSettings(settings);
            EnsureOfficeProfile(settings);
            AddressableAssetGroup group = EnsureOfficeGroup(settings);
            int registered = RegisterSceneEntries(settings, group);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Addressables",
                $"Office Safety module configured.\n\n" +
                $"Group: {OfficeGroupName}\n" +
                $"Scenes registered: {registered}/{RequiredSceneEntries.Length}\n" +
                $"Download label: {DownloadLabel}\n" +
                $"Remote load path: {RemoteLoadPath}",
                "OK");
        }

        static void EnsureRemoteCatalogSettings(AddressableAssetSettings settings)
        {
            settings.BuildRemoteCatalog = true;
            settings.EnableJsonCatalog = true;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.CatalogRequestsTimeout = 10;
            settings.OverridePlayerVersion = ModuleVersion;
            foreach (NamedBuildTarget buildTarget in JsonCatalogBuildTargets)
                EnsureJsonCatalogScriptingDefine(buildTarget);

            if (!settings.GetLabels().Contains(DownloadLabel))
                settings.AddLabel(DownloadLabel);

            const string legacyDownloadLabel = "module-office-safety-full";
            if (!settings.GetLabels().Contains(legacyDownloadLabel))
                settings.AddLabel(legacyDownloadLabel);
        }

        static readonly NamedBuildTarget[] JsonCatalogBuildTargets =
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.EmbeddedLinux,
            NamedBuildTarget.iOS,
            NamedBuildTarget.LinuxHeadlessSimulation,
            NamedBuildTarget.NintendoSwitch,
            NamedBuildTarget.PS4,
            NamedBuildTarget.QNX,
            NamedBuildTarget.Server,
            NamedBuildTarget.Standalone,
            NamedBuildTarget.tvOS,
            NamedBuildTarget.WebGL,
            NamedBuildTarget.WindowsStoreApps,
            NamedBuildTarget.XboxOne,
        };

        static void EnsureJsonCatalogScriptingDefine(NamedBuildTarget buildTarget)
        {
            const string symbol = "ENABLE_JSON_CATALOG";
            PlayerSettings.GetScriptingDefineSymbols(buildTarget, out string[] symbols);
            if (symbols.Contains(symbol))
                return;

            var updated = new string[symbols.Length + 1];
            updated[0] = symbol;
            symbols.CopyTo(updated, 1);
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, updated);
        }

        static void EnsureOfficeProfile(AddressableAssetSettings settings)
        {
            AddressableAssetProfileSettings profile = settings.profileSettings;
            string profileId = profile.GetProfileId(OfficeProfileName);
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = profile.AddProfile(OfficeProfileName, settings.activeProfileId);
            }

            profile.SetValue(profileId, AddressableAssetSettings.kRemoteBuildPath, RemoteBuildPath);
            profile.SetValue(profileId, AddressableAssetSettings.kRemoteLoadPath, RemoteLoadPath);
            settings.activeProfileId = profileId;
        }

        static AddressableAssetGroup EnsureOfficeGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = settings.FindGroup(OfficeGroupName);
            if (group != null)
                return group;

            group = settings.CreateGroup(
                OfficeGroupName,
                false,
                false,
                true,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));

            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundled.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;
            }

            return group;
        }

        static int RegisterSceneEntries(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            int registered = 0;
            var problems = new List<string>();

            foreach ((string guid, string address) in RequiredSceneEntries)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    problems.Add($"{address}: GUID {guid} not found — copy scene assets into this project first.");
                    continue;
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = address;
                entry.SetLabel(DownloadLabel, true, true);
                registered++;
            }

            if (problems.Count > 0)
                Debug.LogWarning("[OfficeAddressablesSetup] Missing assets:\n" + string.Join("\n", problems));

            EditorUtility.SetDirty(settings);
            return registered;
        }

        [MenuItem("Woi/Addressables/Validate Office Remote Build (CRC)", priority = 21)]
        public static void ValidateOfficeRemoteBuildCrc()
        {
            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", "Office", "StandaloneWindows64");
            string catalogPath = Path.Combine(buildDir, $"catalog_{ModuleVersion}.json");

            if (!File.Exists(catalogPath))
            {
                EditorUtility.DisplayDialog(
                    "Addressables",
                    $"Catalog not found:\n{catalogPath}\n\nRun Addressables → New Build → Default Build Script first.",
                    "OK");
                return;
            }

            UnityAddressables.InitializeAsync().WaitForCompletion();
            var catalogHandle = UnityAddressables.LoadContentCatalogAsync(catalogPath);
            catalogHandle.WaitForCompletion();

            if (catalogHandle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded
                || catalogHandle.Result == null)
            {
                EditorUtility.DisplayDialog("Addressables", "Failed to load local catalog for CRC validation.", "OK");
                if (catalogHandle.IsValid())
                    UnityAddressables.Release(catalogHandle);
                return;
            }

            var seenBundles = new HashSet<string>();
            int checkedCount = 0;
            int failedCount = 0;
            var failures = new List<string>();

            foreach (IResourceLocation location in EnumerateLocations(catalogHandle.Result))
            {
                if (location.Data is not AssetBundleRequestOptions options)
                    continue;

                string internalId = location.InternalId ?? string.Empty;
                if (!internalId.EndsWith(".bundle", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(internalId);
                if (!seenBundles.Add(fileName))
                    continue;

                string localPath = Path.Combine(buildDir, fileName);
                if (!File.Exists(localPath))
                {
                    failedCount++;
                    failures.Add($"{fileName}: missing local file");
                    continue;
                }

                checkedCount++;
                AssetBundle bundle = AssetBundle.LoadFromFile(localPath, options.Crc);
                if (bundle == null)
                {
                    failedCount++;
                    failures.Add($"{fileName}: CRC mismatch (catalog expects {options.Crc:x8})");
                    Debug.LogError(
                        $"[OfficeAddressablesSetup] CRC mismatch for '{fileName}'. " +
                        $"Catalog CRC={options.Crc:x8}, size={new FileInfo(localPath).Length} bytes. " +
                        "Rebuild Addressables and upload ALL ServerData files together.",
                        AssetDatabase.LoadMainAssetAtPath("Assets/AddressableAssetsData"));
                    continue;
                }

                bundle.Unload(true);
                Debug.Log($"[OfficeAddressablesSetup] CRC OK: {fileName} ({options.Crc:x8})");
            }

            UnityAddressables.Release(catalogHandle);

            string summary = failedCount == 0
                ? $"All {checkedCount} remote bundle(s) match catalog CRC.\n\nUpload every file in:\n{buildDir}\nto:\n{RemoteLoadPath}/"
                : $"{failedCount} bundle(s) failed CRC check.\n\n" + string.Join("\n", failures) +
                  "\n\nFix: Addressables → New Build → Default Build Script, then re-upload ALL files in ServerData.";

            EditorUtility.DisplayDialog(
                failedCount == 0 ? "CRC validation passed" : "CRC validation failed",
                summary,
                "OK");
        }

        [MenuItem("Woi/Addressables/Log Office Firebase Upload File List", priority = 22)]
        public static void LogOfficeFirebaseUploadFileList()
        {
            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", "Office", "StandaloneWindows64");
            if (!Directory.Exists(buildDir))
            {
                Debug.LogError($"[OfficeAddressablesSetup] Build folder not found: {buildDir}");
                return;
            }

            Debug.Log($"[OfficeAddressablesSetup] Upload ALL of these to {RemoteLoadPath}/ (binary, no compression):");
            foreach (string file in Directory.GetFiles(buildDir).OrderBy(path => path))
            {
                var info = new FileInfo(file);
                Debug.Log($"  {info.Name}  ({info.Length:N0} bytes)");
            }

            Debug.Log(
                "[OfficeAddressablesSetup] Never upload catalog without bundles (or vice versa) from different builds. " +
                "CRC Mismatch in Hub means Firebase has a catalog/bundle pair from mixed builds.");
        }

        static IEnumerable<IResourceLocation> EnumerateLocations(UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator locator)
        {
            foreach (object key in locator.Keys)
            {
                if (!locator.Locate(key, typeof(object), out IList<IResourceLocation> locations) || locations == null)
                    continue;

                for (int i = 0; i < locations.Count; i++)
                {
                    if (locations[i] != null)
                        yield return locations[i];
                }
            }
        }
    }
}
#endif
