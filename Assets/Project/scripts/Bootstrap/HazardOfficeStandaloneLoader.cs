using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Woi.HazardSystem.Compat
{
    /// <summary>
    /// Loads the Hazard Office scene for standalone play.
    /// Uses the player build while Office is enabled there. If that entry is gone, loads HazardHunt_Office.
    /// </summary>
    public static class HazardOfficeStandaloneLoader
    {
        public const string BuiltInSceneName = "Office";
        public const string AddressableKey = "HazardHunt_Office";

        static AsyncOperationHandle<SceneInstance> _addressableScene;

        public static IEnumerator Load(string builtInSceneName, Action<float> reportProgress, HazardOfficeLoadRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(builtInSceneName))
                builtInSceneName = BuiltInSceneName;

            if (Woi.Settings.HazardHubLaunch.IsHubLaunch)
            {
                yield return LoadAddressable(reportProgress, request);
                yield break;
            }

            if (Application.CanStreamedLevelBeLoaded(builtInSceneName))
            {
                Debug.Log("[HazardOfficeStandaloneLoader] '" + builtInSceneName + "' is in the player build. Using SceneManager.");
                AsyncOperation operation = SceneManager.LoadSceneAsync(builtInSceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    request.Fail("SceneManager could not start loading '" + builtInSceneName + "'.");
                    yield break;
                }

                while (!operation.isDone)
                {
                    reportProgress?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
                    yield return null;
                }

                reportProgress?.Invoke(1f);
                request.Succeed();
                yield break;
            }

            if (!string.Equals(builtInSceneName, BuiltInSceneName, StringComparison.Ordinal))
            {
                request.Fail(
                    "Scene '" + builtInSceneName + "' is not in the player build, and there is no Addressables fallback for that name.");
                yield break;
            }

            yield return LoadAddressable(reportProgress, request);
        }

        public static IEnumerator LoadAddressable(Action<float> reportProgress, HazardOfficeLoadRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            Debug.Log("[HazardOfficeStandaloneLoader] Using Addressables key '" + AddressableKey + "'.");
            AsyncOperationHandle init = Addressables.InitializeAsync();
            while (!init.IsDone)
                yield return null;

            if (init.Status != AsyncOperationStatus.Succeeded)
            {
                if (init.IsValid())
                    Addressables.Release(init);
                request.Fail("Addressables.InitializeAsync failed, so '" + AddressableKey + "' cannot be loaded.");
                yield break;
            }

            if (init.IsValid())
                Addressables.Release(init);

            AsyncOperationHandle<IList<IResourceLocation>> locations = Addressables.LoadResourceLocationsAsync(AddressableKey);
            while (!locations.IsDone)
                yield return null;

            bool found = locations.Status == AsyncOperationStatus.Succeeded && locations.Result != null && locations.Result.Count > 0;
            if (locations.IsValid())
                Addressables.Release(locations);

            if (!found)
            {
                request.Fail(
                    "Office is not enabled in Build Settings and Addressables has no location for '" + AddressableKey + "'. " +
                    "Standalone cannot continue. Re-enable Office.unity in Build Settings or load the Hazard catalog before this scene.");
                yield break;
            }

            if (_addressableScene.IsValid())
                Addressables.Release(_addressableScene);

            _addressableScene = Addressables.LoadSceneAsync(AddressableKey, LoadSceneMode.Single, true);
            while (!_addressableScene.IsDone)
            {
                reportProgress?.Invoke(Mathf.Clamp01(_addressableScene.PercentComplete));
                yield return null;
            }

            if (_addressableScene.Status != AsyncOperationStatus.Succeeded)
            {
                string error = "Addressables.LoadSceneAsync('" + AddressableKey + "') failed.";
                if (_addressableScene.IsValid())
                    Addressables.Release(_addressableScene);
                request.Fail(error);
                yield break;
            }

            reportProgress?.Invoke(1f);
            request.Succeed();
        }

        /// <summary>
        /// Hub Start loads Office with <see cref="LoadSceneMode.Single"/>, outside SceneGroupManager.
        /// That leaves Office as the base scene, so a later group load of HazardHunt_Login cannot unload it.
        /// Call this only after HazardHunt_Login is loaded and active.
        /// </summary>
        public static async Task UnloadAddressableOfficeIfLoaded()
        {
            if (!_addressableScene.IsValid())
                return;

            AsyncOperationHandle<SceneInstance> handle = _addressableScene;
            _addressableScene = default;
            try
            {
                AsyncOperationHandle<SceneInstance> unload = Addressables.UnloadSceneAsync(handle);
                await unload.Task;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HazardOfficeStandaloneLoader] Could not unload Hazard Office: " + exception.Message);
            }
        }
    }

    public sealed class HazardOfficeLoadRequest
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }

        public void Succeed()
        {
            Succeeded = true;
            Error = null;
        }

        public void Fail(string error)
        {
            Succeeded = false;
            Error = error ?? "Hazard Office load failed.";
            Debug.LogError("[HazardOfficeStandaloneLoader] " + Error);
        }
    }
}
