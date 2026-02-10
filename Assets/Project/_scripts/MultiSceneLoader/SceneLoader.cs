using System;
using System.Threading.Tasks;
using Systems.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Woi.Settings
{
	public class SceneLoader : MonoBehaviour, ISceneLoaderService
	{
		[SerializeField] private Image progressBar;
		[SerializeField] private float fillSpeed = 0.5f;
		[SerializeField] private Canvas loadingCanvas;
		[SerializeField] private Camera loadingCamera;
		[SerializeField] private SceneGroup[] sceneGroups;

		private float targetProgress;
		private bool isLoading;
		private const float delay = 1;
		private const float beforeDelayScene = 1000f;
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

		private void Update()
		{
			if (!isLoading) return;

			progressBar.fillAmount = Mathf.MoveTowards(
				progressBar.fillAmount,
				targetProgress,
				fillSpeed * Time.deltaTime
			);
		}

		public async Task LoadSceneGroup(int index)
		{
			progressBar.fillAmount = 0f;
			targetProgress = 1f;

			if (index < 0 || index >= sceneGroups.Length)
			{
				Debug.LogError("Invalid scene group index: " + index);
				return;
			}

			LoadingProgress progress = new LoadingProgress();
			progress.Progressed += target => targetProgress = Mathf.Max(target, targetProgress);

			EnableLoadingCanvas();
			
			await manager.LoadScenes(sceneGroups[index], progress, false);

			await Task.Delay((int)beforeDelayScene); //A little delay to ensure progress bar reaches 100%	

			EnableLoadingCanvas(false);
		}

		private void EnableLoadingCanvas(bool enable = true)
		{
			isLoading = enable;
			loadingCanvas.gameObject.SetActive(enable);
			loadingCamera.gameObject.SetActive(enable);
		}

		private int GetGroupIndex(string name) => Array.FindIndex(sceneGroups, g => g.GroupName == name);
	}

	public interface ISceneLoaderService
	{
		public Task LoadScene(string name);
	}
}




