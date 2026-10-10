#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Woi.Editor.Addressables
{
    /// <summary>
    /// Builds one Office module catalog at a time from the single AddressableAssetSettings asset.
    /// Every Addressables setting and Build Settings scene flag touched for the invocation is restored.
    /// This does not upload.
    /// </summary>
    public static class OfficeModuleAddressablesBuild
    {
        const string Bucket = "https://storage.googleapis.com/digitech-hub-388ab.firebasestorage.app";
        const string FireVersion = "0.3.0";
        const string HazardVersion = "0.1.2";
        const string WasteVersion = "0.1.1";
        const string RemoteBuildVariable = "Remote.BuildPath";
        const string RemoteLoadVariable = "Remote.LoadPath";
        const string HazardBuildVariable = "Hazard.BuildPath";
        const string HazardLoadPcVariable = "Hazard.LoadPath.PC";
        const string HazardLoadVrVariable = "Hazard.LoadPath.VR";
        const string WasteBuildVariable = "Waste.BuildPath";
        const string WasteLoadPcVariable = "Waste.LoadPath.PC";
        const string WasteLoadVrVariable = "Waste.LoadPath.VR";
        const string SafetyGroup = "Office_Safety";
        const string HazardGroup = "Office_Hazard";
        const string WasteGroup = "Office_Waste";
        const string DefaultGroup = "Default Local Group";
        const string OfficeScenePath = "Assets/Project/Scenes/Office.unity";

        const string FireBuildPath = "ServerData/Office/[BuildTarget]";
        const string HazardBuildPath = "ServerData/OfficeHazard/[BuildTarget]";
        const string WasteBuildPath = "ServerData/OfficeWaste/[BuildTarget]";

        static readonly string[] HazardRequiredAddresses =
        {
            "Hazard_Boot",
            "HazardHunt_Login",
            "HazardHunt_Office",
            "Managers/InputManager",
            "Managers/SceneLoader",
        };

        static readonly string[] WasteRequiredAddresses =
        {
            "Waste_Boot",
            "WasteLogin",
            "Waste_Office",
            "Managers/InputManager",
            "Managers/SceneLoader",
        };

        static readonly string[] HazardSceneAddresses =
        {
            "Hazard_Boot",
            "HazardHunt_Login",
            "HazardHunt_Office",
        };

        static readonly string[] FireSceneAddresses =
        {
            "Office_Boot",
            "OfficeFireModule_Login",
            "FireModule_Office",
            "FireStairsOfis",
            "FireStairsOfis 1",
            "OutDoor",
        };

        enum ModuleKind
        {
            Fire,
            Hazard,
            Waste,
        }

        enum PlatformKind
        {
            Pc,
            Vr,
        }

        readonly struct BuildSpec
        {
            public readonly ModuleKind Module;
            public readonly PlatformKind Platform;
            public readonly BuildTarget BuildTarget;
            public readonly string Version;
            public readonly string LoadUrl;
            public readonly string CatalogBuildVariable;
            public readonly string CatalogLoadVariable;
            public readonly string ContentStateDirectory;

            public BuildSpec(ModuleKind module, PlatformKind platform)
            {
                Module = module;
                Platform = platform;
                BuildTarget = platform == PlatformKind.Vr ? BuildTarget.Android : BuildTarget.StandaloneWindows64;
                Version = module switch
                {
                    ModuleKind.Hazard => HazardVersion,
                    ModuleKind.Waste => WasteVersion,
                    _ => FireVersion,
                };
                string platformSegment = platform == PlatformKind.Vr ? "vr" : "pc";
                string moduleSegment = module switch
                {
                    ModuleKind.Hazard => "hazard",
                    ModuleKind.Waste => "waste",
                    _ => "fire",
                };
                LoadUrl = Bucket + "/modules/office/" + moduleSegment + "/" + platformSegment + "/" + Version;
                CatalogBuildVariable = module switch
                {
                    ModuleKind.Hazard => HazardBuildVariable,
                    ModuleKind.Waste => WasteBuildVariable,
                    _ => RemoteBuildVariable,
                };
                CatalogLoadVariable = module switch
                {
                    ModuleKind.Hazard => platform == PlatformKind.Vr ? HazardLoadVrVariable : HazardLoadPcVariable,
                    ModuleKind.Waste => platform == PlatformKind.Vr ? WasteLoadVrVariable : WasteLoadPcVariable,
                    _ => RemoteLoadVariable,
                };
                string contentFolder = module switch
                {
                    ModuleKind.Hazard => "OfficeHazard",
                    ModuleKind.Waste => "OfficeWaste",
                    _ => "Office",
                };
                ContentStateDirectory = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "ServerData",
                    contentFolder,
                    "_content_state");
            }

            public string Title => Module + " " + (Platform == PlatformKind.Vr ? "VR" : "PC");
        }

        sealed class SceneFlag
        {
            public string Path;
            public bool Enabled;
        }

        sealed class ModuleBuildState
        {
            public BuildTarget ActiveBuildTarget;
            public string ActiveProfileId;
            public string ActiveProfileName;
            public int ActiveBuilderIndex;
            public string OverridePlayerVersion;
            public string RemoteCatalogBuildVariable;
            public string RemoteCatalogLoadVariable;
            public string RemoteCatalogBuildEvaluated;
            public string RemoteCatalogLoadEvaluated;
            public string RemoteBuildPathValue;
            public string RemoteLoadPathValue;
            public string HazardBuildPathValue;
            public string HazardLoadPcValue;
            public string HazardLoadVrValue;
            public string WasteBuildPathValue;
            public string WasteLoadPcValue;
            public string WasteLoadVrValue;
            public string ContentStateBuildPath;
            public string CustomContentStateBuildPath;
            public bool SafetyInclude;
            public string SafetyBuildVariable;
            public string SafetyLoadVariable;
            public bool HazardInclude;
            public string HazardBuildVariable;
            public string HazardLoadVariable;
            public bool WasteInclude;
            public string WasteBuildVariable;
            public string WasteLoadVariable;
            public bool DefaultInclude;
            public string DefaultBuildVariable;
            public string DefaultLoadVariable;
            public bool OfficeSceneEnabled;
            public string HazardOfficeAddress;

            public string Format()
            {
                return
                    "activeBuildTarget=" + ActiveBuildTarget + "\n" +
                    "activeProfile=" + ActiveProfileName + "\n" +
                    "activeBuilderIndex=" + ActiveBuilderIndex + "\n" +
                    "PlayerBuildVersion=" + OverridePlayerVersion + "\n" +
                    "RemoteCatalogBuildPath=" + RemoteCatalogBuildVariable + " | " + RemoteCatalogBuildEvaluated + "\n" +
                    "RemoteCatalogLoadPath=" + RemoteCatalogLoadVariable + " | " + RemoteCatalogLoadEvaluated + "\n" +
                    "profile.Remote.BuildPath=" + RemoteBuildPathValue + "\n" +
                    "profile.Remote.LoadPath=" + RemoteLoadPathValue + "\n" +
                    "profile.Hazard.BuildPath=" + HazardBuildPathValue + "\n" +
                    "profile.Hazard.LoadPath.PC=" + HazardLoadPcValue + "\n" +
                    "profile.Hazard.LoadPath.VR=" + HazardLoadVrValue + "\n" +
                    "profile.Waste.BuildPath=" + WasteBuildPathValue + "\n" +
                    "profile.Waste.LoadPath.PC=" + WasteLoadPcValue + "\n" +
                    "profile.Waste.LoadPath.VR=" + WasteLoadVrValue + "\n" +
                    "Office_Safety include=" + SafetyInclude + " build=" + SafetyBuildVariable + " load=" + SafetyLoadVariable + "\n" +
                    "Office_Hazard include=" + HazardInclude + " build=" + HazardBuildVariable + " load=" + HazardLoadVariable + "\n" +
                    "Office_Waste include=" + WasteInclude + " build=" + WasteBuildVariable + " load=" + WasteLoadVariable + "\n" +
                    "Default Local Group include=" + DefaultInclude + " build=" + DefaultBuildVariable + " load=" + DefaultLoadVariable + "\n" +
                    "contentStateBuildPath=" + ContentStateBuildPath + "\n" +
                    "customContentStateBuildPath=" + CustomContentStateBuildPath + "\n" +
                    "officeSceneEnabled=" + OfficeSceneEnabled + "\n" +
                    "hazardOfficeAddress=" + HazardOfficeAddress + "\n";
            }
        }

        [MenuItem("Woi/Addressables/Build Office Module/Fire PC", priority = 40)]
        public static void BuildFirePc() => Run(new BuildSpec(ModuleKind.Fire, PlatformKind.Pc));

        [MenuItem("Woi/Addressables/Build Office Module/Fire VR", priority = 41)]
        public static void BuildFireVr() => Run(new BuildSpec(ModuleKind.Fire, PlatformKind.Vr));

        [MenuItem("Woi/Addressables/Build Office Module/Hazard PC", priority = 42)]
        public static void BuildHazardPc() => Run(new BuildSpec(ModuleKind.Hazard, PlatformKind.Pc));

        [MenuItem("Woi/Addressables/Build Office Module/Hazard VR", priority = 43)]
        public static void BuildHazardVr() => Run(new BuildSpec(ModuleKind.Hazard, PlatformKind.Vr));

        [MenuItem("Woi/Addressables/Build Office Module/Waste PC", priority = 44)]
        public static void BuildWastePc() => Run(new BuildSpec(ModuleKind.Waste, PlatformKind.Pc));

        [MenuItem("Woi/Addressables/Build Office Module/Waste VR", priority = 45)]
        public static void BuildWasteVr() => Run(new BuildSpec(ModuleKind.Waste, PlatformKind.Vr));

        static void Run(BuildSpec spec)
        {
            bool succeeded = false;
            string summary = spec.Title + " build did not finish.";
            try
            {
                succeeded = Execute(spec, out summary);
            }
            catch (Exception exception)
            {
                summary = spec.Title + " build threw: " + exception.Message;
                Debug.LogException(exception);
            }

            if (succeeded)
                Debug.Log("[OfficeModuleBuild] " + summary);
            else
                Debug.LogError("[OfficeModuleBuild] " + summary);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    succeeded ? "Office module build passed" : "Office module build failed",
                    summary,
                    "OK");
            }
            else
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        static bool Execute(BuildSpec spec, out string summary)
        {
            summary = spec.Title + " build failed.";
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                summary = "AddressableAssetSettings was not found.";
                return false;
            }

            if (settings.profileSettings.GetProfileName(settings.activeProfileId) != "Office")
            {
                summary = "Active Addressables profile must be Office before this build. It is '" +
                          settings.profileSettings.GetProfileName(settings.activeProfileId) + "'.";
                return false;
            }

            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            ModuleBuildState before = Capture(settings);
            List<SceneFlag> sceneFlags = SnapshotSceneFlags();
            string outputDirectory = null;
            bool buildSucceeded = false;
            var validationErrors = new List<string>();
            Exception failure = null;
            Delegate suppressedSceneListHandler = null;

            try
            {
                suppressedSceneListHandler = SuppressAddressableSceneListHandler();
                Apply(settings, spec);
                AssetDatabase.SaveAssets();

                if (EditorUserBuildSettings.activeBuildTarget != spec.BuildTarget)
                {
                    Debug.Log("[OfficeModuleBuild] Switching build target to " + spec.BuildTarget + ".");
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(spec.BuildTarget), spec.BuildTarget))
                        throw new InvalidOperationException("Could not switch the active build target to " + spec.BuildTarget + ".");
                }

                AddressablesRuntimeProperties.ClearCachedPropertyValues();

                if (Application.isBatchMode)
                    EditorSceneManager.SaveOpenScenes();

                Debug.Log("[OfficeModuleBuild] Building " + spec.Title + " catalog " + spec.Version + ".");
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                if (result == null || !string.IsNullOrEmpty(result.Error))
                    throw new InvalidOperationException(result == null ? "Addressables build returned no result." : result.Error);

                outputDirectory = settings.RemoteCatalogBuildPath.GetValue(settings);
                if (!string.IsNullOrEmpty(outputDirectory) && !Path.IsPathRooted(outputDirectory))
                    outputDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputDirectory));
                buildSucceeded = true;
                validationErrors.AddRange(Validate(spec, outputDirectory));
            }
            catch (Exception exception)
            {
                failure = exception;
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AddressableAssetSettings restoredSettings = AddressableAssetSettingsDefaultObject.Settings ?? settings;
                RestoreSettings(restoredSettings, before);
                RestoreBuildTarget(before.ActiveBuildTarget);
                AddressablesRuntimeProperties.ClearCachedPropertyValues();
                RestoreSceneFlags(sceneFlags);
                ResumeAddressableSceneListHandler(suppressedSceneListHandler);

                ModuleBuildState after = Capture(AddressableAssetSettingsDefaultObject.Settings ?? restoredSettings);
                List<string> mismatches = Diff(before, after);
                bool restored = mismatches.Count == 0;
                if (!restored)
                {
                    string mismatchText = "Restore mismatch:\n" + string.Join("\n", mismatches);
                    Debug.LogError("[OfficeModuleBuild] " + mismatchText);
                    validationErrors.Add(mismatchText);
                }
                else
                {
                    Debug.Log("[OfficeModuleBuild] Settings restored.\n" + after.Format());
                }

                WriteReports(outputDirectory, spec, before.Format(), after.Format(), restored, validationErrors, failure);
            }

            if (failure != null)
            {
                summary = spec.Title + " build failed: " + failure.Message + " Output was left in place for inspection.";
                return false;
            }

            if (!buildSucceeded || validationErrors.Count > 0)
            {
                summary = spec.Title + " validation failed (" + validationErrors.Count + "):\n" + string.Join("\n", validationErrors);
                return false;
            }

            summary = spec.Title + " build passed. Catalog: " + outputDirectory + "/catalog_" + spec.Version + ".json";
            return true;
        }

        static void Apply(AddressableAssetSettings settings, BuildSpec spec)
        {
            EnsureProfileValue(settings, RemoteBuildVariable, FireBuildPath);
            EnsureProfileValue(settings, HazardBuildVariable, HazardBuildPath);
            EnsureProfileValue(settings, HazardLoadPcVariable, Bucket + "/modules/office/hazard/pc/" + HazardVersion);
            EnsureProfileValue(settings, HazardLoadVrVariable, Bucket + "/modules/office/hazard/vr/" + HazardVersion);
            EnsureProfileValue(settings, WasteBuildVariable, WasteBuildPath);
            EnsureProfileValue(settings, WasteLoadPcVariable, Bucket + "/modules/office/waste/pc/" + WasteVersion);
            EnsureProfileValue(settings, WasteLoadVrVariable, Bucket + "/modules/office/waste/vr/" + WasteVersion);

            string fireLoadUrl = spec.Module == ModuleKind.Fire
                ? spec.LoadUrl
                : Bucket + "/modules/office/fire/pc/" + FireVersion;
            EnsureProfileValue(settings, RemoteLoadVariable, fireLoadUrl);

            settings.OverridePlayerVersion = spec.Version;
            if (!settings.RemoteCatalogBuildPath.SetVariableByName(settings, spec.CatalogBuildVariable))
                throw new InvalidOperationException("Could not set the remote catalog build path to " + spec.CatalogBuildVariable + ".");
            if (!settings.RemoteCatalogLoadPath.SetVariableByName(settings, spec.CatalogLoadVariable))
                throw new InvalidOperationException("Could not set the remote catalog load path to " + spec.CatalogLoadVariable + ".");

            settings.ContentStateBuildPath = spec.ContentStateDirectory;

            bool hazard = spec.Module == ModuleKind.Hazard;
            bool waste = spec.Module == ModuleKind.Waste;
            string moduleBuildVariable = waste ? WasteBuildVariable : hazard ? HazardBuildVariable : RemoteBuildVariable;
            string moduleLoadVariable = hazard || waste ? spec.CatalogLoadVariable : RemoteLoadVariable;

            ConfigureGroup(settings, SafetyGroup, include: spec.Module == ModuleKind.Fire, RemoteBuildVariable, RemoteLoadVariable);
            ConfigureGroup(settings, HazardGroup, include: hazard, HazardBuildVariable, hazard ? spec.CatalogLoadVariable : HazardLoadPcVariable);
            ConfigureGroup(settings, WasteGroup, include: waste, WasteBuildVariable, waste ? spec.CatalogLoadVariable : WasteLoadPcVariable);
            ConfigureGroup(settings, DefaultGroup, include: true, moduleBuildVariable, moduleLoadVariable);

            EditorUtility.SetDirty(settings);
            Debug.Log(
                "[OfficeModuleBuild] Applied " + spec.Title +
                " version=" + spec.Version +
                " catalogBuild=" + spec.CatalogBuildVariable +
                " catalogLoad=" + spec.CatalogLoadVariable +
                " loadUrl=" + spec.LoadUrl);
        }

        static bool EnsureProfileValue(AddressableAssetSettings settings, string variableName, string expected)
        {
            string current = settings.profileSettings.GetValueByName(settings.activeProfileId, variableName);
            if (current == expected)
                return false;

            settings.profileSettings.SetValue(settings.activeProfileId, variableName, expected);
            return true;
        }

        static bool ConfigureGroup(AddressableAssetSettings settings, string groupName, bool include, string buildVariable, string loadVariable)
        {
            BundledAssetGroupSchema schema = Schema(settings, groupName);
            bool changed = false;
            if (schema.IncludeInBuild != include)
            {
                schema.IncludeInBuild = include;
                changed = true;
            }

            if (schema.BuildPath.GetName(settings) != buildVariable)
            {
                if (!schema.BuildPath.SetVariableByName(settings, buildVariable))
                    throw new InvalidOperationException(groupName + " build path variable '" + buildVariable + "' was not found.");
                changed = true;
            }

            if (schema.LoadPath.GetName(settings) != loadVariable)
            {
                if (!schema.LoadPath.SetVariableByName(settings, loadVariable))
                    throw new InvalidOperationException(groupName + " load path variable '" + loadVariable + "' was not found.");
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(schema);

            return changed;
        }

        static List<string> Validate(BuildSpec spec, string outputDirectory)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                errors.Add("Catalog output folder was not created: " + outputDirectory);
                return errors;
            }

            string catalogPath = Path.Combine(outputDirectory, "catalog_" + spec.Version + ".json");
            string hashPath = Path.Combine(outputDirectory, "catalog_" + spec.Version + ".hash");
            if (!File.Exists(catalogPath))
                errors.Add("Missing catalog file: " + catalogPath);
            if (!File.Exists(hashPath))
                errors.Add("Missing catalog hash: " + hashPath);
            if (errors.Count > 0)
                return errors;

            string json = File.ReadAllText(catalogPath);
            string[] forbiddenPrefixes = spec.Module switch
            {
                ModuleKind.Waste => new[] { "modules/office/fire/", "modules/office/hazard/" },
                ModuleKind.Hazard => new[] { "modules/office/fire/", "modules/office/waste/" },
                _ => new[] { "modules/office/hazard/", "modules/office/waste/" },
            };
            foreach (string forbiddenPrefix in forbiddenPrefixes)
            {
                if (json.IndexOf(forbiddenPrefix, StringComparison.Ordinal) >= 0)
                    errors.Add("Catalog text references " + forbiddenPrefix);
            }

            ContentCatalogData catalog = DeserializeCatalog(json);
            if (catalog == null)
            {
                errors.Add("Could not read catalog JSON: " + catalogPath);
                return errors;
            }

            var addresses = new HashSet<string>(StringComparer.Ordinal);
            var internalIds = new List<string>();
            if (catalog.InternalIds != null)
                internalIds.AddRange(catalog.InternalIds);

            try
            {
                IResourceLocator locator = catalog.CreateLocator();
                foreach (object key in locator.Keys)
                {
                    if (key is string address)
                        addresses.Add(address);

                    if (!locator.Locate(key, typeof(object), out IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation> locations) || locations == null)
                        continue;

                    foreach (UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation location in locations)
                        CollectInternalIds(location, internalIds, new HashSet<string>(StringComparer.Ordinal));
                }
            }
            catch (Exception exception)
            {
                errors.Add("Could not read catalog addresses: " + exception.Message);
            }

            if (spec.Module == ModuleKind.Waste)
            {
                foreach (string required in WasteRequiredAddresses)
                {
                    if (!addresses.Contains(required))
                        errors.Add("Missing required address '" + required + "'.");
                }

                foreach (string fireAddress in FireSceneAddresses)
                {
                    if (addresses.Contains(fireAddress))
                        errors.Add("Fire scene address is present in the Waste catalog: '" + fireAddress + "'.");
                }

                foreach (string hazardAddress in HazardSceneAddresses)
                {
                    if (addresses.Contains(hazardAddress))
                        errors.Add("Hazard scene address is present in the Waste catalog: '" + hazardAddress + "'.");
                }
            }
            else if (spec.Module == ModuleKind.Hazard)
            {
                foreach (string required in HazardRequiredAddresses)
                {
                    if (!addresses.Contains(required))
                        errors.Add("Missing required address '" + required + "'.");
                }

                foreach (string fireAddress in FireSceneAddresses)
                {
                    if (addresses.Contains(fireAddress))
                        errors.Add("Fire scene address is present in the Hazard catalog: '" + fireAddress + "'.");
                }

                foreach (string wasteAddress in WasteRequiredAddresses)
                {
                    if (wasteAddress.StartsWith("Managers/", StringComparison.Ordinal))
                        continue;
                    if (addresses.Contains(wasteAddress))
                        errors.Add("Waste scene address is present in the Hazard catalog: '" + wasteAddress + "'.");
                }
            }
            else
            {
                foreach (string fireAddress in FireSceneAddresses)
                {
                    if (!addresses.Contains(fireAddress))
                        errors.Add("Missing Fire address '" + fireAddress + "'.");
                }

                if (addresses.Contains("Hazard_Boot") || addresses.Contains("HazardHunt_Office"))
                    errors.Add("Hazard scene addresses are present in the Fire catalog.");

                foreach (string wasteAddress in WasteRequiredAddresses)
                {
                    if (wasteAddress.StartsWith("Managers/", StringComparison.Ordinal))
                        continue;
                    if (addresses.Contains(wasteAddress))
                        errors.Add("Waste scene address is present in the Fire catalog: '" + wasteAddress + "'.");
                }
            }

            foreach (string forbiddenPrefix in forbiddenPrefixes)
            {
                foreach (string internalId in internalIds.Distinct())
                {
                    if (internalId != null && internalId.IndexOf(forbiddenPrefix, StringComparison.Ordinal) >= 0)
                        errors.Add("Bundle location references " + forbiddenPrefix + " : " + internalId);
                }
            }

            int bundleCount = Directory.GetFiles(outputDirectory, "*.bundle", SearchOption.TopDirectoryOnly).Length;
            Debug.Log(
                "[OfficeModuleBuild] Catalog " + catalogPath +
                " addresses=" + addresses.Count +
                " bundles=" + bundleCount +
                " validationErrors=" + errors.Count);

            if (errors.Count > 0)
                Debug.LogError("[OfficeModuleBuild] Validation failed:\n" + string.Join("\n", errors));

            return errors;
        }

        static void CollectInternalIds(
            UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation location,
            List<string> internalIds,
            HashSet<string> seen)
        {
            if (location == null || string.IsNullOrEmpty(location.InternalId) || !seen.Add(location.InternalId))
                return;

            internalIds.Add(location.InternalId);
            if (location.Dependencies == null)
                return;

            foreach (UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation dependency in location.Dependencies)
                CollectInternalIds(dependency, internalIds, seen);
        }

        static ContentCatalogData DeserializeCatalog(string json)
        {
            try
            {
                var catalog = (ContentCatalogData)FormatterServices.GetUninitializedObject(typeof(ContentCatalogData));
                JsonUtility.FromJsonOverwrite(json, catalog);
                return catalog;
            }
            catch (Exception exception)
            {
                Debug.LogError("[OfficeModuleBuild] Catalog JSON parse failed: " + exception.Message);
                return null;
            }
        }

        static ModuleBuildState Capture(AddressableAssetSettings settings)
        {
            var serialized = new SerializedObject(settings);
            BundledAssetGroupSchema safety = Schema(settings, SafetyGroup);
            BundledAssetGroupSchema hazard = Schema(settings, HazardGroup);
            BundledAssetGroupSchema waste = Schema(settings, WasteGroup);
            BundledAssetGroupSchema defaults = Schema(settings, DefaultGroup);
            string officeGuid = AssetDatabase.AssetPathToGUID(OfficeScenePath);
            AddressableAssetEntry officeEntry = settings.FindAssetEntry(officeGuid);
            bool officeEnabled = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == OfficeScenePath && scene.enabled)
                {
                    officeEnabled = true;
                    break;
                }
            }

            return new ModuleBuildState
            {
                ActiveBuildTarget = EditorUserBuildSettings.activeBuildTarget,
                ActiveProfileId = settings.activeProfileId,
                ActiveProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId),
                ActiveBuilderIndex = settings.ActivePlayerDataBuilderIndex,
                OverridePlayerVersion = settings.OverridePlayerVersion,
                RemoteCatalogBuildVariable = settings.RemoteCatalogBuildPath.GetName(settings),
                RemoteCatalogLoadVariable = settings.RemoteCatalogLoadPath.GetName(settings),
                RemoteCatalogBuildEvaluated = settings.RemoteCatalogBuildPath.GetValue(settings),
                RemoteCatalogLoadEvaluated = settings.RemoteCatalogLoadPath.GetValue(settings),
                RemoteBuildPathValue = settings.profileSettings.GetValueByName(settings.activeProfileId, RemoteBuildVariable),
                RemoteLoadPathValue = settings.profileSettings.GetValueByName(settings.activeProfileId, RemoteLoadVariable),
                HazardBuildPathValue = settings.profileSettings.GetValueByName(settings.activeProfileId, HazardBuildVariable),
                HazardLoadPcValue = settings.profileSettings.GetValueByName(settings.activeProfileId, HazardLoadPcVariable),
                HazardLoadVrValue = settings.profileSettings.GetValueByName(settings.activeProfileId, HazardLoadVrVariable),
                WasteBuildPathValue = settings.profileSettings.GetValueByName(settings.activeProfileId, WasteBuildVariable),
                WasteLoadPcValue = settings.profileSettings.GetValueByName(settings.activeProfileId, WasteLoadPcVariable),
                WasteLoadVrValue = settings.profileSettings.GetValueByName(settings.activeProfileId, WasteLoadVrVariable),
                ContentStateBuildPath = serialized.FindProperty("m_ContentStateBuildPath").stringValue,
                CustomContentStateBuildPath = serialized.FindProperty("m_CustomContentStateBuildPath").stringValue,
                SafetyInclude = safety.IncludeInBuild,
                SafetyBuildVariable = safety.BuildPath.GetName(settings),
                SafetyLoadVariable = safety.LoadPath.GetName(settings),
                HazardInclude = hazard.IncludeInBuild,
                HazardBuildVariable = hazard.BuildPath.GetName(settings),
                HazardLoadVariable = hazard.LoadPath.GetName(settings),
                WasteInclude = waste.IncludeInBuild,
                WasteBuildVariable = waste.BuildPath.GetName(settings),
                WasteLoadVariable = waste.LoadPath.GetName(settings),
                DefaultInclude = defaults.IncludeInBuild,
                DefaultBuildVariable = defaults.BuildPath.GetName(settings),
                DefaultLoadVariable = defaults.LoadPath.GetName(settings),
                OfficeSceneEnabled = officeEnabled,
                HazardOfficeAddress = officeEntry != null ? officeEntry.address : "<missing>",
            };
        }

        static void RestoreSettings(AddressableAssetSettings settings, ModuleBuildState snapshot)
        {
            bool changed = false;
            if (settings.activeProfileId != snapshot.ActiveProfileId)
            {
                settings.activeProfileId = snapshot.ActiveProfileId;
                changed = true;
            }

            if (settings.ActivePlayerDataBuilderIndex != snapshot.ActiveBuilderIndex)
            {
                settings.ActivePlayerDataBuilderIndex = snapshot.ActiveBuilderIndex;
                changed = true;
            }

            if (settings.OverridePlayerVersion != snapshot.OverridePlayerVersion)
            {
                settings.OverridePlayerVersion = snapshot.OverridePlayerVersion;
                changed = true;
            }

            if (settings.RemoteCatalogBuildPath.GetName(settings) != snapshot.RemoteCatalogBuildVariable)
            {
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, snapshot.RemoteCatalogBuildVariable);
                changed = true;
            }

            if (settings.RemoteCatalogLoadPath.GetName(settings) != snapshot.RemoteCatalogLoadVariable)
            {
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, snapshot.RemoteCatalogLoadVariable);
                changed = true;
            }

            changed |= EnsureProfileValue(settings, RemoteBuildVariable, snapshot.RemoteBuildPathValue);
            changed |= EnsureProfileValue(settings, RemoteLoadVariable, snapshot.RemoteLoadPathValue);
            changed |= EnsureProfileValue(settings, HazardBuildVariable, snapshot.HazardBuildPathValue);
            changed |= EnsureProfileValue(settings, HazardLoadPcVariable, snapshot.HazardLoadPcValue);
            changed |= EnsureProfileValue(settings, HazardLoadVrVariable, snapshot.HazardLoadVrValue);
            changed |= EnsureProfileValue(settings, WasteBuildVariable, snapshot.WasteBuildPathValue);
            changed |= EnsureProfileValue(settings, WasteLoadPcVariable, snapshot.WasteLoadPcValue);
            changed |= EnsureProfileValue(settings, WasteLoadVrVariable, snapshot.WasteLoadVrValue);
            changed |= ConfigureGroup(settings, SafetyGroup, snapshot.SafetyInclude, snapshot.SafetyBuildVariable, snapshot.SafetyLoadVariable);
            changed |= ConfigureGroup(settings, HazardGroup, snapshot.HazardInclude, snapshot.HazardBuildVariable, snapshot.HazardLoadVariable);
            changed |= ConfigureGroup(settings, WasteGroup, snapshot.WasteInclude, snapshot.WasteBuildVariable, snapshot.WasteLoadVariable);
            changed |= ConfigureGroup(settings, DefaultGroup, snapshot.DefaultInclude, snapshot.DefaultBuildVariable, snapshot.DefaultLoadVariable);

            var serialized = new SerializedObject(settings);
            SerializedProperty contentState = serialized.FindProperty("m_ContentStateBuildPath");
            SerializedProperty customContentState = serialized.FindProperty("m_CustomContentStateBuildPath");
            string contentStateValue = snapshot.ContentStateBuildPath ?? string.Empty;
            string customContentStateValue = snapshot.CustomContentStateBuildPath ?? string.Empty;
            if (contentState.stringValue != contentStateValue || customContentState.stringValue != customContentStateValue)
            {
                contentState.stringValue = contentStateValue;
                customContentState.stringValue = customContentStateValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (!changed)
                return;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        static BundledAssetGroupSchema Schema(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                throw new InvalidOperationException("Addressables group '" + groupName + "' was not found.");

            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                throw new InvalidOperationException("Group '" + groupName + "' has no bundled schema.");

            return schema;
        }

        static List<string> Diff(ModuleBuildState before, ModuleBuildState after)
        {
            var mismatches = new List<string>();
            Compare(mismatches, "active build target", before.ActiveBuildTarget.ToString(), after.ActiveBuildTarget.ToString());
            Compare(mismatches, "active profile", before.ActiveProfileName, after.ActiveProfileName);
            Compare(mismatches, "active builder index", before.ActiveBuilderIndex.ToString(), after.ActiveBuilderIndex.ToString());
            Compare(mismatches, "PlayerBuildVersion", before.OverridePlayerVersion, after.OverridePlayerVersion);
            Compare(mismatches, "RemoteCatalogBuildPath", before.RemoteCatalogBuildVariable, after.RemoteCatalogBuildVariable);
            Compare(mismatches, "RemoteCatalogBuildPath evaluated", before.RemoteCatalogBuildEvaluated, after.RemoteCatalogBuildEvaluated);
            Compare(mismatches, "RemoteCatalogLoadPath", before.RemoteCatalogLoadVariable, after.RemoteCatalogLoadVariable);
            Compare(mismatches, "RemoteCatalogLoadPath evaluated", before.RemoteCatalogLoadEvaluated, after.RemoteCatalogLoadEvaluated);
            Compare(mismatches, "profile Remote.BuildPath", before.RemoteBuildPathValue, after.RemoteBuildPathValue);
            Compare(mismatches, "profile Remote.LoadPath", before.RemoteLoadPathValue, after.RemoteLoadPathValue);
            Compare(mismatches, "profile Hazard.BuildPath", before.HazardBuildPathValue, after.HazardBuildPathValue);
            Compare(mismatches, "profile Hazard.LoadPath.PC", before.HazardLoadPcValue, after.HazardLoadPcValue);
            Compare(mismatches, "profile Hazard.LoadPath.VR", before.HazardLoadVrValue, after.HazardLoadVrValue);
            Compare(mismatches, "profile Waste.BuildPath", before.WasteBuildPathValue, after.WasteBuildPathValue);
            Compare(mismatches, "profile Waste.LoadPath.PC", before.WasteLoadPcValue, after.WasteLoadPcValue);
            Compare(mismatches, "profile Waste.LoadPath.VR", before.WasteLoadVrValue, after.WasteLoadVrValue);
            Compare(mismatches, "content state build path", before.ContentStateBuildPath, after.ContentStateBuildPath);
            Compare(mismatches, "custom content state build path", before.CustomContentStateBuildPath, after.CustomContentStateBuildPath);
            Compare(mismatches, "Office_Safety.IncludeInBuild", before.SafetyInclude.ToString(), after.SafetyInclude.ToString());
            Compare(mismatches, "Office_Safety.BuildPath", before.SafetyBuildVariable, after.SafetyBuildVariable);
            Compare(mismatches, "Office_Safety.LoadPath", before.SafetyLoadVariable, after.SafetyLoadVariable);
            Compare(mismatches, "Office_Hazard.IncludeInBuild", before.HazardInclude.ToString(), after.HazardInclude.ToString());
            Compare(mismatches, "Office_Hazard.BuildPath", before.HazardBuildVariable, after.HazardBuildVariable);
            Compare(mismatches, "Office_Hazard.LoadPath", before.HazardLoadVariable, after.HazardLoadVariable);
            Compare(mismatches, "Office_Waste.IncludeInBuild", before.WasteInclude.ToString(), after.WasteInclude.ToString());
            Compare(mismatches, "Office_Waste.BuildPath", before.WasteBuildVariable, after.WasteBuildVariable);
            Compare(mismatches, "Office_Waste.LoadPath", before.WasteLoadVariable, after.WasteLoadVariable);
            Compare(mismatches, "Default Local Group.IncludeInBuild", before.DefaultInclude.ToString(), after.DefaultInclude.ToString());
            Compare(mismatches, "Default Local Group.BuildPath", before.DefaultBuildVariable, after.DefaultBuildVariable);
            Compare(mismatches, "Default Local Group.LoadPath", before.DefaultLoadVariable, after.DefaultLoadVariable);
            Compare(mismatches, "Office.unity enabled in Build Settings", before.OfficeSceneEnabled.ToString(), after.OfficeSceneEnabled.ToString());
            Compare(mismatches, "HazardHunt_Office address", before.HazardOfficeAddress, after.HazardOfficeAddress);
            return mismatches;
        }

        static void Compare(List<string> mismatches, string label, string before, string after)
        {
            if (before == after)
                return;

            mismatches.Add(label + ":\n  before = " + before + "\n  after  = " + after);
        }

        static List<SceneFlag> SnapshotSceneFlags()
        {
            return EditorBuildSettings.scenes
                .Select(scene => new SceneFlag { Path = scene.path, Enabled = scene.enabled })
                .ToList();
        }

        static void RestoreSceneFlags(List<SceneFlag> snapshot)
        {
            if (snapshot == null)
                return;

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            bool changed = current.Length != snapshot.Count;
            if (!changed)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i].path != snapshot[i].Path || current[i].enabled != snapshot[i].Enabled)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return;

            EditorBuildSettings.scenes = snapshot
                .Select(scene => new EditorBuildSettingsScene(scene.Path, scene.Enabled))
                .ToArray();
            Debug.LogWarning("[OfficeModuleBuild] Restored Editor Build Settings scene flags after the content build.");
        }

        static Delegate SuppressAddressableSceneListHandler()
        {
            FieldInfo field = SceneListHandlerField();
            if (field == null)
            {
                Debug.LogWarning("[OfficeModuleBuild] Could not find BuiltinSceneCache.sceneListChanged. Build Settings restore may remove the Hazard Office address.");
                return null;
            }

            var handler = (Delegate)field.GetValue(null);
            field.SetValue(null, null);
            return handler;
        }

        static void ResumeAddressableSceneListHandler(Delegate handler)
        {
            FieldInfo field = SceneListHandlerField();
            if (field == null)
                return;

            field.SetValue(null, handler);
        }

        static FieldInfo SceneListHandlerField()
        {
            Type cache = typeof(AddressableAssetSettings).Assembly.GetType("UnityEditor.AddressableAssets.Settings.BuiltinSceneCache");
            return cache?.GetField("sceneListChanged", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        static void RestoreBuildTarget(BuildTarget originalTarget)
        {
            if (EditorUserBuildSettings.activeBuildTarget == originalTarget)
                return;

            Debug.Log("[OfficeModuleBuild] Restoring build target to " + originalTarget + ".");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(originalTarget), originalTarget);
        }

        static void WriteReports(
            string outputDirectory,
            BuildSpec spec,
            string before,
            string after,
            bool restored,
            List<string> validationErrors,
            Exception failure)
        {
            if (string.IsNullOrEmpty(outputDirectory))
                return;

            try
            {
                Directory.CreateDirectory(outputDirectory);
                var report = new StringBuilder();
                report.Append(spec.Title).Append('\n');
                report.Append("restored=").Append(restored).Append('\n');
                if (failure != null)
                    report.Append("buildError=").Append(failure.Message).Append('\n');
                report.Append("validationErrors=").Append(validationErrors.Count).Append('\n');
                foreach (string error in validationErrors)
                    report.Append("- ").Append(error).Append('\n');
                report.Append("\nBEFORE\n").Append(before).Append("\nAFTER\n").Append(after);
                File.WriteAllText(Path.Combine(outputDirectory, "office-module-build-report.txt"), report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[OfficeModuleBuild] Could not write the build report: " + exception.Message);
            }
        }
    }
}
#endif
