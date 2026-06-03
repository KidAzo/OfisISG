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
			await UnloadScenes();
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
						Debug.LogError($"[SGM] LoadSceneAsync returned NULL for '{sceneData.SceneName}' — is it in Build Settings?");
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
			
			Debug.Log("[SGM] Invoking OnSceneGroupLoaded");
			OnSceneGroupLoaded.Invoke();
			Debug.Log("[SGM] LoadScenes RETURN (group fully loaded)");

			//EventBus.Publish(new OnSceneGroupLoaded());
		}

		public async UniTask UnloadScenes()
		{
			var scenes = new List<string>();
			var activeScene = SceneManager.GetActiveScene().name;

			int sceneCount = SceneManager.sceneCount;

			for (var i = sceneCount - 1; i > 0; i--)
			{
				var sceneAt = SceneManager.GetSceneAt(i);
				if (!sceneAt.isLoaded) continue;

				var sceneName = sceneAt.name;
				if (sceneName == bootstrapperSceneName) continue;
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

			foreach (var handle in handleGroup.Handles)
			{
				if (handle.IsValid())
				{
					Addressables.UnloadSceneAsync(handle);
				}
			}
			handleGroup.Handles.Clear();

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

		public float Progress => Operations.Count == 0 ? 0 : Operations.Average(o => o.progress);
		public bool IsDone => Operations.All(o => o.isDone);

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