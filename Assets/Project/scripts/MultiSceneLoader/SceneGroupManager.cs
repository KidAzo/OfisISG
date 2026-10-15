using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Woi.Settings;
using WoiUtils.AudioSystem;

namespace Systems.SceneManagement
{
	public class SceneGroupManager
	{
		public event Action<SceneSettings, SceneData> OnSceneLoaded = delegate { };
		public event Action<string> OnSceneUnloaded = delegate { };
		public event Action OnSceneGroupLoaded = delegate { };

		readonly AsyncOperationHandleGroup handleGroup = new AsyncOperationHandleGroup(10);

		SceneGroup ActiveSceneGroup;

		const float delayBetweenSceneLoads = 2f;	
		const string bootstrapperSceneName = "Bootstrapper";

		public async UniTask LoadScenes(SceneGroup group, IProgress<float> progress, bool reloadDupScenes = false)
		{
			ActiveSceneGroup = group;
			var loadedScenes = new List<string>();

			Debug.Log($"[SGM] LoadScenes START group='{group.GroupName}' scenesInGroup={group.Scenes?.Count ?? 0} reloadDup={reloadDupScenes} loadedCount={SceneManager.sceneCount}");

			StopWoiAudioForSceneTransition();

			Debug.Log("[SGM] UnloadScenes BEGIN");
			// OutDoor must load while FireModule_Office (and its XR rig) is still loaded.
			// The assembly transition unloads that set only after OutDoor is up, and awaits it.
			// Returning to login is the reverse: OutDoor stays until login is the active scene,
			// because Unity will not unload the active scene.
			bool deferOutdoorUnload = string.Equals(group.GroupName, LoginSceneGroupName, StringComparison.Ordinal)
				&& SceneManager.GetSceneByName(OutdoorSceneName).isLoaded;
			HashSet<string> keepLoaded = string.Equals(group.GroupName, "OutDoor", StringComparison.Ordinal)
				? new HashSet<string>(FireOfficeScenesKeptUntilOutdoorLoaded)
				: null;
			if (deferOutdoorUnload)
			{
				keepLoaded ??= new HashSet<string>();
				keepLoaded.Add(OutdoorSceneName);
			}
			await UnloadScenes(keepLoaded);
			Debug.Log($"[SGM] UnloadScenes DONE, now loadedCount={SceneManager.sceneCount}");

			int sceneCount = SceneManager.sceneCount;

			for (var i = 0; i < sceneCount; i++)
			{
				loadedScenes.Add(SceneManager.GetSceneAt(i).name);
			}

			var totalScenesToLoad = ActiveSceneGroup.Scenes.Count;

			var operationGroup = new AsyncOperationGroup(totalScenesToLoad);

			for (var i = 0; i < totalScenesToLoad; i++)
			{
				var sceneData = group.Scenes[i];
				if (reloadDupScenes == false && loadedScenes.Contains(sceneData.Name))
				{
					Debug.Log($"[SGM] Skipping already-loaded scene '{sceneData.Name}'");
					continue;
				}

				if (!string.IsNullOrEmpty(sceneData.AddressableKey))
				{
					Debug.Log($"[SGM] Addressables.LoadSceneAsync key='{sceneData.AddressableKey}'");
					var sceneHandle = Addressables.LoadSceneAsync(sceneData.AddressableKey, LoadSceneMode.Additive);
					handleGroup.Handles.Add(sceneHandle);
				}
				else if (!string.IsNullOrEmpty(sceneData.SceneName))
				{
					Debug.Log($"[SGM] SceneManager.LoadSceneAsync name='{sceneData.SceneName}'");
					var operation = SceneManager.LoadSceneAsync(sceneData.SceneName, LoadSceneMode.Additive);
					if (operation == null)
					{
						Debug.LogError($"[SGM] LoadSceneAsync returned NULL for '{sceneData.SceneName}' — is it in Build Settings?");
						continue;
					}
					operationGroup.Operations.Add(operation);
				}
				else
				{
					Debug.LogWarning(
						$"[SceneGroupManager] Scene entry {i} in group '{group.GroupName}' has no SceneName or AddressableKey — skipping.");
					continue;
				}

				OnSceneLoaded.Invoke(group.sceneSettings, sceneData);
				await Task.Delay(TimeSpan.FromSeconds(delayBetweenSceneLoads)); 
			}

			// Wait until all AsyncOperations in the group are done
			Debug.Log($"[SGM] Wait loop START ops={operationGroup.Operations.Count} handles={handleGroup.Handles.Count}");
			int _sgmIter = 0;
			while (!operationGroup.IsDone || !handleGroup.IsDone)
			{
				progress?.Report((operationGroup.Progress + handleGroup.Progress) / 2);

				if (_sgmIter % 10 == 0)
				{
					string opStates = string.Join(",", operationGroup.Operations.ConvertAll(o =>
						o == null ? "NULL" : $"{o.progress:0.00}/done={o.isDone}/act={o.allowSceneActivation}"));
					Debug.Log($"[SGM] waiting iter={_sgmIter} opDone={operationGroup.IsDone} handleDone={handleGroup.IsDone} ops=[{opStates}]");
				}
				_sgmIter++;
				await Task.Delay(100);
			}
			Debug.Log("[SGM] Wait loop END (all operations/handles done)");

			Scene activeScene = SceneManager.GetSceneByName(ActiveSceneGroup.FindSceneNameByType(SceneType.ActiveScene));
			Debug.Log($"[SGM] ActiveScene resolve name='{ActiveSceneGroup.FindSceneNameByType(SceneType.ActiveScene)}' valid={activeScene.IsValid()}");

			if (activeScene.IsValid())
			{
				SceneManager.SetActiveScene(activeScene);
			}

			if (deferOutdoorUnload)
			{
				Debug.Log(
					"[OfficeFireReturn]\nstage=login-loaded\nactiveScene=" + SceneManager.GetActiveScene().name);
				await UnloadOutDoorAfterLoginActiveAsync();
			}
			
			Debug.Log("[SGM] Invoking OnSceneGroupLoaded");
			OnSceneGroupLoaded.Invoke();
			Debug.Log("[SGM] LoadScenes RETURN (group fully loaded)");

			//EventBus.Publish(new OnSceneGroupLoaded());
		}

		const string OutdoorSceneName = "OutDoor";
		const string LoginSceneGroupName = "OfficeFireModule_Login";

		static readonly string[] FireOfficeScenesKeptUntilOutdoorLoaded =
		{
			"FireModule_Office",
			"FireStairsOfis",
			"FireStairsOfis 1",
		};

		/// <summary>
		/// Login is already loaded and active. Releases the OutDoor handle owned by this manager
		/// and waits until the scene is gone. Used only by the result-screen return path.
		/// </summary>
		async UniTask UnloadOutDoorAfterLoginActiveAsync()
		{
			Scene login = SceneManager.GetSceneByName(LoginSceneGroupName);
			if (login.IsValid() && login.isLoaded && SceneManager.GetActiveScene() != login)
				SceneManager.SetActiveScene(login);

			bool outdoorLoaded = SceneManager.GetSceneByName(OutdoorSceneName).isLoaded;
			Debug.Log(
				"[OfficeFireReturn]\nstage=outdoor-unload-begin\noutdoorLoaded=" +
				(outdoorLoaded ? "true" : "false"));

			var keptHandles = new List<AsyncOperationHandle<SceneInstance>>();
			var pendingUnloads = new List<AsyncOperationHandle<SceneInstance>>();
			foreach (var handle in handleGroup.Handles)
			{
				if (!handle.IsValid())
					continue;

				bool isOutdoor = TryGetHandleSceneName(handle, out string handleSceneName)
					&& handleSceneName == OutdoorSceneName;
				Debug.Log(
					"[OfficeFireReturn]\nstage=outdoor-handle\n" +
					"matchesOutDoor=" + (isOutdoor ? "true" : "false") +
					"\nscene=" + (handleSceneName ?? string.Empty) +
					"\nvalid=" + handle.IsValid() +
					"\nstatus=" + handle.Status +
					"\nisDone=" + handle.IsDone);

				if (isOutdoor)
					pendingUnloads.Add(handle);
				else
					keptHandles.Add(handle);
			}

			if (pendingUnloads.Count == 0)
			{
				Debug.Log("[OfficeFireReturn]\nstage=outdoor-handle\nowned=false");
			}

			handleGroup.Handles.Clear();
			handleGroup.Handles.AddRange(keptHandles);

			Scene outdoor = SceneManager.GetSceneByName(OutdoorSceneName);
			if (outdoor.IsValid() && outdoor.isLoaded && SceneManager.GetActiveScene() == outdoor)
			{
				handleGroup.Handles.AddRange(pendingUnloads);
				Debug.LogError(
					"[OfficeFireReturn] OutDoor is still the active scene. Login must be active before it can unload.");
			}
			else
			{
				if (pendingUnloads.Count > 0)
				{
					var tasks = new List<UniTask>(pendingUnloads.Count);
					for (int i = 0; i < pendingUnloads.Count; i++)
						tasks.Add(Addressables.UnloadSceneAsync(pendingUnloads[i]).ToUniTask());

					await UniTask.WhenAll(tasks);
				}

				outdoor = SceneManager.GetSceneByName(OutdoorSceneName);
				if (outdoor.IsValid() && outdoor.isLoaded && SceneManager.GetActiveScene() != outdoor)
				{
					AsyncOperation operation = SceneManager.UnloadSceneAsync(outdoor);
					if (operation != null)
					{
						OnSceneUnloaded.Invoke(OutdoorSceneName);
						await operation.ToUniTask();
					}
				}
			}

			outdoorLoaded = SceneManager.GetSceneByName(OutdoorSceneName).isLoaded;
			Debug.Log(
				"[OfficeFireReturn]\nstage=outdoor-unload-complete\noutdoorLoaded=" +
				(outdoorLoaded ? "true" : "false"));
			if (outdoorLoaded)
			{
				Debug.LogError("[OfficeFireReturn] OutDoor is still loaded after the return unload.");
			}
		}

		public async UniTask UnloadScenes(HashSet<string> scenesToKeep = null)
		{
			var scenes = new List<string>();

			int sceneCount = SceneManager.sceneCount;

			for (var i = sceneCount - 1; i > 0; i--)
			{
				var sceneAt = SceneManager.GetSceneAt(i);
				if (!sceneAt.isLoaded) continue;

				var sceneName = sceneAt.name;
				if (sceneName == bootstrapperSceneName) continue;
				if (scenesToKeep != null && scenesToKeep.Contains(sceneName)) continue;
				if (handleGroup.Handles.Any(h => h.IsValid() && h.Result.Scene.name == sceneName)) continue;

				scenes.Add(sceneName);
			}

			// Create an AsyncOperationGroup
			var operationGroup = new AsyncOperationGroup(scenes.Count);

			foreach (var scene in scenes)
			{
				var operation = SceneManager.UnloadSceneAsync(scene);
				if (operation == null) continue;

				operationGroup.Operations.Add(operation);

				OnSceneUnloaded.Invoke(scene);
			}

			var keptHandles = new List<AsyncOperationHandle<SceneInstance>>();
			foreach (var handle in handleGroup.Handles)
			{
				if (!handle.IsValid())
					continue;

				if (scenesToKeep != null && TryGetHandleSceneName(handle, out string handleSceneName) && scenesToKeep.Contains(handleSceneName))
				{
					keptHandles.Add(handle);
					continue;
				}

				Addressables.UnloadSceneAsync(handle);
			}
			handleGroup.Handles.Clear();
			handleGroup.Handles.AddRange(keptHandles);

			// Wait until all AsyncOperations in the group are done
			while (!operationGroup.IsDone)
			{
				await UniTask.Delay(100); // delay to avoid tight loop
			}

			// Optional: UnloadUnusedAssets - unloads all unused assets from memory
			await Resources.UnloadUnusedAssets();

			//EventBus.Publish(new OnSceneGroupUnloaded());	
		}

		/// <summary>
		/// Unloads named scenes and waits until each unload finishes.
		/// Addressable handles owned by this manager are released with Addressables and awaited.
		/// Scenes loaded outside Addressables use SceneManager and are awaited the same way.
		/// </summary>
		public async Task UnloadScenesByNameAsync(IReadOnlyList<string> sceneNames)
		{
			if (sceneNames == null || sceneNames.Count == 0)
				return;

			var unloadNames = new HashSet<string>(sceneNames);
			Scene outdoor = SceneManager.GetSceneByName("OutDoor");
			if (outdoor.IsValid() && outdoor.isLoaded && SceneManager.GetActiveScene() != outdoor)
				SceneManager.SetActiveScene(outdoor);

			var keptHandles = new List<AsyncOperationHandle<SceneInstance>>();
			var pendingUnloads = new List<AsyncOperationHandle<SceneInstance>>();
			foreach (var handle in handleGroup.Handles)
			{
				if (!handle.IsValid())
					continue;

				if (TryGetHandleSceneName(handle, out string handleSceneName) && unloadNames.Contains(handleSceneName))
					pendingUnloads.Add(handle);
				else
					keptHandles.Add(handle);
			}

			handleGroup.Handles.Clear();
			handleGroup.Handles.AddRange(keptHandles);

			var addressableUnloaded = new HashSet<string>();
			var tasks = new List<UniTask>(pendingUnloads.Count);
			for (int i = 0; i < pendingUnloads.Count; i++)
			{
				if (TryGetHandleSceneName(pendingUnloads[i], out string sceneName))
					addressableUnloaded.Add(sceneName);

				tasks.Add(Addressables.UnloadSceneAsync(pendingUnloads[i]).ToUniTask());
			}

			if (tasks.Count > 0)
				await UniTask.WhenAll(tasks);

			for (int i = 0; i < sceneNames.Count; i++)
			{
				string sceneName = sceneNames[i];
				if (string.IsNullOrEmpty(sceneName) || addressableUnloaded.Contains(sceneName))
					continue;

				Scene scene = SceneManager.GetSceneByName(sceneName);
				if (!scene.IsValid() || !scene.isLoaded)
					continue;
				if (SceneManager.GetActiveScene() == scene)
					continue;

				AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
				if (operation == null)
					continue;

				OnSceneUnloaded.Invoke(sceneName);
				await operation.ToUniTask();
			}
		}

		static bool TryGetHandleSceneName(AsyncOperationHandle<SceneInstance> handle, out string sceneName)
		{
			sceneName = null;
			if (!handle.IsValid() || !handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded)
				return false;

			Scene scene = handle.Result.Scene;
			if (!scene.IsValid())
				return false;

			sceneName = scene.name;
			return !string.IsNullOrEmpty(sceneName);
		}

		/// <summary>
		/// Stops all <see cref="AudioSystem"/> voices and queued one-shots so level VO/SFX from the previous group
		/// do not continue over the loading screen or into the next scene.
		/// </summary>
		static void StopWoiAudioForSceneTransition()
		{
			if (AudioSystem.TryGetFromServiceLocator(out AudioSystem registered) && registered != null)
			{
				registered.StopAll();
				return;
			}

			AudioSystem fallback = UnityEngine.Object.FindFirstObjectByType<AudioSystem>();
			if (fallback != null)
				fallback.StopAll();
		}
	}

	public readonly struct AsyncOperationGroup
	{
		public readonly List<AsyncOperation> Operations;

		public float Progress => Operations.Count == 0 ? 0 : Operations.Average(o => o != null ? o.progress : 1f);
		public bool IsDone => Operations.All(o => o == null || o.isDone);

		public AsyncOperationGroup(int initialCapacity)
		{
			Operations = new List<AsyncOperation>(initialCapacity);
		}
	}

	public readonly struct AsyncOperationHandleGroup
	{
		public readonly List<AsyncOperationHandle<SceneInstance>> Handles;

		public float Progress => Handles.Count == 0 ? 0 : Handles.Average(h => h.PercentComplete);
		public bool IsDone => Handles.Count == 0 || Handles.All(o => o.IsDone);

		public AsyncOperationHandleGroup(int initialCapacity)
		{
			Handles = new List<AsyncOperationHandle<SceneInstance>>(initialCapacity);
		}
	}
}