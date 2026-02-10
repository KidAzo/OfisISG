using System;
using System.Threading.Tasks;
using Systems.SceneManagement;
using UnityEngine;

namespace Woi.Settings
{
	public class SceneLoader : MonoBehaviour, ISceneLoaderService
	{
		//[SerializeField] private ProgressBar progressBar;
		[SerializeField] private float fillSpeed = 0.5f;
		[SerializeField] private Canvas loadingCanvas;
		[SerializeField] private Camera loadingCamera;
		[SerializeField] private SceneGroup[] sceneGroups;

		private float targetProgress;
		private const float progressHandler = 0.1f;
		private bool isLoading;
		private const float delay = 1;
		public readonly SceneGroupManager manager = new();
		private int currentSceneGroupID = 0;	
		public void SetCurrentSceneGroupId(int id) => currentSceneGroupID = id;

		public async Task LoadScene(string name)
		{
			int index = GetGroupIndex(name);
			await LoadSceneGroup(index);
		}

		public async Task LoadScene(int index)
		{
			await LoadSceneGroup(index);
		}

		public async Task LoadSceneFromID()
		{
			await LoadSceneGroup(currentSceneGroupID);
		}

		//[Button]
		//public async Task LoadCrusherScene()
		//{
		//	await LoadSceneGroup(0);
		//}

		//[Button]
		//public async Task LoadVRMScene()
		//{
		//	await LoadSceneGroup(1);
		//}

		//private async void Start()
		//{
		//	await LoadScene("CrusherSceneGroup");
		//}

		private void Update()
		{
			if (!isLoading) return;

			// float currentValue = progressBar.currentPercent;
			// float progressDifference = Mathf.Abs(currentValue - targetProgress);
			
			// float dynamicFillSpeed = progressDifference * fillSpeed * progressHandler;

			// float value = Mathf.Lerp(currentValue, targetProgress, Time.deltaTime * dynamicFillSpeed);
			// progressBar.SetValue(value);
		}

		public async Task LoadSceneGroup(int index)
		{
			//progressBar.SetValue(0.0f);
			targetProgress = 100f;

			if (index < 0 || index >= sceneGroups.Length)
			{
				Debug.LogError("Invalid scene group index: " + index);
				return;
			}

			LoadingProgress progress = new LoadingProgress();
			progress.Progressed += target => targetProgress = Mathf.Max(target, targetProgress);

			EnableLoadingCanvas();
			
			await manager.LoadScenes(sceneGroups[index], progress, false);

			await Task.Delay(100); //A little delay to ensure progress bar reaches 100%	

			EnableLoadingCanvas(false);
		}

		private void EnableLoadingCanvas(bool enable = true)
		{
			isLoading = enable;
			loadingCanvas.gameObject.SetActive(enable);
		}

		private int GetGroupIndex(string name) => Array.FindIndex(sceneGroups, g => g.GroupName == name);
	}

	public interface ISceneLoaderService
	{
		public Task LoadScene(string name);
	}
}




