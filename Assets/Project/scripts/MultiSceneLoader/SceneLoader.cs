using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Systems.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Woi.Settings
{
	public enum SceneLoadPresentation
	{
		/// <summary>Loading canvas, optional progress bar, and XR black fade.</summary>
		Default = 0,

		/// <summary>No loading canvas or progress bar. XR uses black fade only when configured.</summary>
		FadeOnly = 1,

		/// <summary>No loading UI. Caller handles fade/transition.</summary>
		Silent = 2,
	}

	public class SceneLoader : MonoBehaviour, ISceneLoaderService
	{
		LoadingScreenController.LoadingScreenSettings loadingScreenSettings;

		[SerializeField] private SceneGroup[] sceneGroups;

		[Header("Addressables catalog health (before load)")]
		[Tooltip("When enabled, resolves critical Addressables keys before loading this scene group (prevents silent freezes from stale cache).")]
		[SerializeField] private bool _runCatalogHealthBeforeSelectedGroups = true;

		[Tooltip("Scene group GroupName values that require catalog health checks (e.g. Fire training gameplay).")]
		[SerializeField] private string[] _catalogHealthGroupNames =
		{
			"Fire_Train",
			"WasteLogin",
			"WasteCollector",
		};

		[SerializeField] private string[] _catalogHealthRequiredKeys =
		{
			"Managers/SceneLoader",
			"Managers/InputManager",
			"Managers/PortingVariable",
			"Managers/PC-GameplayContext",
		};

		[SerializeField] private float _catalogHealthTimeoutPerKeySeconds = 15f;
		private float targetProgress;
		private bool isLoading;
		private const float delay = 1;
		private const float beforeDelayScene = 2000f;
		public readonly SceneGroupManager manager = new();
		public SceneGroupManager Manager => manager;
		private int currentSceneGroupID = 0;
		public void SetCurrentSceneGroupId(int id) => currentSceneGroupID = id;

		public Task LoadScene(string name)
		{
			return LoadScene(name, SceneLoadPresentation.Default);
		}

		public async Task LoadScene(string name, SceneLoadPresentation presentation)
		{
			int index = GetGroupIndex(name);
			await LoadSceneGroup(index, presentation);
		}

		public Task LoadScene(int index)
		{
			return LoadSceneGroup(index, SceneLoadPresentation.Default);
		}

		public Task LoadSceneFromID()
		{
			return LoadSceneGroup(currentSceneGroupID, SceneLoadPresentation.Default);
		}
		
		void Awake()
		{
			RefreshLoadingScreenSettings();
		}

		void Start()
		{
			RefreshLoadingScreenSettings();
			GetComponent<LoadingScreenController>()?.HideAllLoadingUi();
		}

		void RefreshLoadingScreenSettings()
		{
			var controller = GetComponent<LoadingScreenController>();
			if (controller != null)
				loadingScreenSettings = controller.CurrentLoadingScreenSettings;
		}

		bool HasLoadingProgressUi =>
			loadingScreenSettings != null
			&& loadingScreenSettings.progressBar != null;

		private void Update()
		{
			if (!isLoading || !HasLoadingProgressUi)
				return;

			loadingScreenSettings.progressBar.fillAmount = Mathf.MoveTowards(
				loadingScreenSettings.progressBar.fillAmount,
				targetProgress,
				loadingScreenSettings.fillSpeed * Time.deltaTime
			);

		}

		public Task LoadSceneGroup(int index)
		{
			return LoadSceneGroup(index, SceneLoadPresentation.Default);
		}

		public async Task LoadSceneGroup(int index, SceneLoadPresentation presentation)
		{
			bool useLoadingCanvas = presentation == SceneLoadPresentation.Default;

			RefreshLoadingScreenSettings();
			if (useLoadingCanvas && HasLoadingProgressUi)
				loadingScreenSettings.progressBar.fillAmount = 0f;
			else if (useLoadingCanvas)
				Debug.LogWarning(
					"[SceneLoader] Loading screen UI is not fully configured (missing XR/PC LoadingScreenSettings or progress bar). Scene load continues without progress bar.");

			targetProgress = 1f;
			isLoading = useLoadingCanvas && HasLoadingProgressUi;

			if (index < 0 || index >= sceneGroups.Length)
			{
				Debug.LogError("Invalid scene group index: " + index);
				return;
			}

			if (presentation == SceneLoadPresentation.Silent)
			{
				var silentGroup = sceneGroups[index];
				LoadingProgress silentProgress = new LoadingProgress();
				await manager.LoadScenes(silentGroup, silentProgress, false);
				return;
			}

			GameObject xrRigRoot = null;
			GameObject xrRigDisabledTarget = null;
			bool manageXrRig = false;
			bool xrRigDisabledForThisLoad = false;
			if (loadingScreenSettings != null && loadingScreenSettings.mode == AppMode.XR)
			{
				xrRigRoot = ResolveXrRigRootForLoad(loadingScreenSettings);
				manageXrRig = xrRigRoot != null;
				if (!manageXrRig)
					Debug.LogWarning(
						"[SceneLoader] XR load: no xrRigRoot assigned and no XROrigin found in loaded scenes — rig will not be toggled during load.");
			}

			CanvasGroup xrFadeOverlay = null;
			float xrFadeIn = 0.35f;
			float xrFadeOut = 0.35f;
			if (loadingScreenSettings != null
			    && loadingScreenSettings.mode == AppMode.XR
			    && loadingScreenSettings.xrBlackFadeOverlay != null)
			{
				xrFadeOverlay = loadingScreenSettings.xrBlackFadeOverlay;
				xrFadeIn = Mathf.Max(0f, loadingScreenSettings.xrFadeInDuration);
				xrFadeOut = Mathf.Max(0f, loadingScreenSettings.xrFadeOutDuration);
			}

			LoadingProgress progress = new LoadingProgress();
			progress.Progressed += target => targetProgress = Mathf.Max(target, targetProgress);

			try
			{
				if (manageXrRig && xrRigRoot.activeSelf)
				{
					xrRigRoot.SetActive(false);
					xrRigDisabledTarget = xrRigRoot;
					xrRigDisabledForThisLoad = true;
				}

				if (xrFadeOverlay != null)
				{
					xrFadeOverlay.alpha = 0f;
					xrFadeOverlay.blocksRaycasts = false;
					xrFadeOverlay.interactable = false;
				}

				if (useLoadingCanvas)
					EnableLoadingCanvas(true);

				if (xrFadeOverlay != null)
					await RunFadeCanvasGroupAsync(xrFadeOverlay, 0f, 1f, xrFadeIn);

				var group = sceneGroups[index];
				if (_runCatalogHealthBeforeSelectedGroups
				    && _catalogHealthGroupNames != null
				    && _catalogHealthGroupNames.Contains(group.GroupName))
				{
					bool healthy = await AddressablesCatalogHealth
						.RunPreGameplayChecksAsync(group.GroupName, _catalogHealthRequiredKeys, _catalogHealthTimeoutPerKeySeconds);
					if (!healthy)
					{
						Debug.LogError(
							"[SceneLoader] Addressables catalog health check reported failures — continuing load anyway; watch for stalls. " +
							"Rebuild Addressables, bump Player version, or clear Caching.");
					}
				}

				Debug.Log($"[SceneLoader] manager.LoadScenes BEGIN group='{group.GroupName}' mode={(loadingScreenSettings != null ? loadingScreenSettings.mode.ToString() : "null")} manageXrRig={manageXrRig}");
				await manager.LoadScenes(group, progress, false);
				Debug.Log("[SceneLoader] manager.LoadScenes RETURNED");

				if (useLoadingCanvas)
				{
					Debug.Log($"[SceneLoader] post-load delay {beforeDelayScene}ms");
					await Task.Delay((int)beforeDelayScene);
				}

				if (manageXrRig)
				{
					Debug.Log("[SceneLoader] EnableXrRigSafe");
					EnableXrRigSafe(xrRigDisabledTarget, loadingScreenSettings);
				}

				if (xrFadeOverlay != null)
				{
					Debug.Log("[SceneLoader] XR fade-out BEGIN");
					await RunFadeCanvasGroupAsync(xrFadeOverlay, 1f, 0f, xrFadeOut);
					Debug.Log("[SceneLoader] XR fade-out END");
				}
			}
			catch (Exception ex)
			{
				if (xrFadeOverlay != null)
				{
					xrFadeOverlay.alpha = 0f;
					xrFadeOverlay.blocksRaycasts = false;
					xrFadeOverlay.interactable = false;
				}

				if (xrRigDisabledForThisLoad)
					EnableXrRigSafe(xrRigDisabledTarget, loadingScreenSettings);

				Debug.LogException(ex);
				throw;
			}
			finally
			{
				isLoading = false;
				if (useLoadingCanvas)
					EnableLoadingCanvas(false);

				GetComponent<LoadingScreenController>()?.RefreshDisplayFallbackCamera();
			}
		}

		Task RunFadeCanvasGroupAsync(CanvasGroup group, float from, float to, float duration)
		{
			var tcs = new TaskCompletionSource<bool>();
			StartCoroutine(FadeCanvasGroupRoutine(group, from, to, duration, () => tcs.SetResult(true)));
			return tcs.Task;
		}

		static IEnumerator FadeCanvasGroupRoutine(CanvasGroup group, float from, float to, float duration, System.Action onComplete)
		{
			if (group == null)
			{
				onComplete?.Invoke();
				yield break;
			}

			group.alpha = from;
			if (duration <= 0f)
			{
				group.alpha = to;
				ApplyCanvasGroupFadeState(group, to);
				onComplete?.Invoke();
				yield break;
			}

			float t = 0f;
			while (t < duration)
			{
				t += Time.unscaledDeltaTime;
				float u = Mathf.Clamp01(t / duration);
				float a = Mathf.Lerp(from, to, u);
				group.alpha = a;
				ApplyCanvasGroupFadeState(group, a);
				yield return null;
			}

			group.alpha = to;
			ApplyCanvasGroupFadeState(group, to);
			onComplete?.Invoke();
		}

		static void ApplyCanvasGroupFadeState(CanvasGroup group, float alpha)
		{
			if (group == null)
				return;
			bool solid = alpha >= 0.99f;
			group.blocksRaycasts = solid;
			group.interactable = solid;
		}

		static void EnableXrRigSafe(GameObject disabledTarget, LoadingScreenController.LoadingScreenSettings settings)
		{
			GameObject rig = disabledTarget;
			if (rig == null)
				rig = ResolveXrRigRootForLoad(settings);
			if (rig != null)
				rig.SetActive(true);
		}

		/// <summary>Uses explicit <see cref="LoadingScreenController.LoadingScreenSettings.xrRigRoot"/> when set; otherwise finds an XROrigin (Unity.XR.CoreUtils) in a loaded scene.</summary>
		static GameObject ResolveXrRigRootForLoad(LoadingScreenController.LoadingScreenSettings settings)
		{
			if (settings == null || settings.mode != AppMode.XR)
				return null;
			if (settings.xrRigRoot != null)
				return settings.xrRigRoot;

			return TryFindXrOriginRootInLoadedScenes();
		}

		static GameObject TryFindXrOriginRootInLoadedScenes()
		{
			Type originType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
			if (originType == null)
				return null;

			Array found = Resources.FindObjectsOfTypeAll(originType);
			for (int i = 0; i < found.Length; i++)
			{
				if (found.GetValue(i) is not Component origin || origin == null)
					continue;
				GameObject go = origin.gameObject;
				if (!go.scene.IsValid() || !go.scene.isLoaded)
					continue;
				return go;
			}

			return null;
		}

		private void EnableLoadingCanvas(bool enable = true)
		{
			Debug.Log("Loading canvas " + (enable ? "enabled" : "disabled"));
			isLoading = enable;
			RefreshLoadingScreenSettings();

			var controller = GetComponent<LoadingScreenController>();
			if (!enable)
			{
				controller?.HideAllLoadingUi();
				return;
			}

			if (loadingScreenSettings == null
			    || loadingScreenSettings.loadingCanvas == null
			    || loadingScreenSettings.loadingCamera == null)
			{
				Debug.LogWarning(
					"[SceneLoader] Cannot toggle loading canvas/camera — LoadingScreenSettings missing or incomplete.");
				return;
			}

			controller?.HideAllLoadingUi();
			loadingScreenSettings.loadingCanvas.gameObject.SetActive(true);
			loadingScreenSettings.loadingCamera.gameObject.SetActive(true);
		}

		private int GetGroupIndex(string name) => Array.FindIndex(sceneGroups, g => g.GroupName == name);
	}

	public interface ISceneLoaderService
	{
		Task LoadScene(string name);

		Task LoadScene(string name, SceneLoadPresentation presentation);

		SceneGroupManager Manager { get; }
	}
}





