using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Events;
using Woi.Player;
using Woi.Porting;

namespace Woi.PopUpSystem
{
	public class PopupManager : MonoBehaviour
	{
		[SerializeField] PopupFactory factory;
		[SerializeField] PopupPoolAdapter popupPoolAdapter;
		[SerializeField] float delayBetweenPopups = 0.5f;

		// VR Settings
		[Header("VR Settings")]
		[SerializeField] bool isVRMode = true;
		[SerializeField] float vrPopupDistance = 2f;
		[SerializeField] Vector3 vrPopupOffset = new Vector3(0, 0.5f, 0);
		[SerializeField] float vrPopupScale = 0.001f;
		[Inject] IXRPlayerService xRPlayerService;

		PopupBuilder popupBuilder = new PopupBuilder();
		Stack<IPopup> activePopups = new Stack<IPopup>();
		Queue<PopupRequest> popupQueue = new Queue<PopupRequest>();
		[SerializeField] Camera vrCamera;
		[Inject] IPortingService portingService;

		BasePopup currentPopup;
		bool isProcessingQueue = false; // isShowingPopup yerine daha açık isim
		CancellationTokenSource queueCts;



		void Start()
		{
			isVRMode = portingService.CurrentMode == AppMode.XR;
			
			queueCts = new CancellationTokenSource();
			EventBus.Subscribe<OnSceneGroupLoaded>(OnSceneLoaded_Event);
			EventBus.Subscribe<OnSceneGroupUnloaded>(UnSeceneUnloaded_Event);
		}

		void UnSeceneUnloaded_Event(OnSceneGroupUnloaded evt)
		{
			OnSceneUnloaded(string.Empty);
		}

		void OnDestroy()
		{
			EventBus.Unsubscribe<OnSceneGroupLoaded>(OnSceneLoaded_Event);
			EventBus.Unsubscribe<OnSceneGroupUnloaded>(UnSeceneUnloaded_Event);
			queueCts?.Cancel();
			queueCts?.Dispose();
		}

		private void OnSceneLoaded_Event(OnSceneGroupLoaded loaded)
		{
			
		}

		public void EnqueuePopup(PopupData data)
		{
			var request = new PopupRequest
			{
				data = data,
				timestamp = Time.time
			};

			popupQueue.Enqueue(request);
			Debug.Log($"[PopupManager] Popup enqueued. Queue count: {popupQueue.Count}, isProcessingQueue: {isProcessingQueue}");

			if (!isProcessingQueue)
			{
				Debug.Log("[PopupManager] Starting ProcessQueue");
				ProcessQueue(queueCts.Token, data.isHazard).Forget();
			}
		}

		public BasePopup CreateInfoPopup(PopupData data, bool isHazard)
		{
			Debug.Log("Creating Info Popup");
			BasePopup popup = factory.CreatePopup(popupPoolAdapter, isHazard);
			activePopups.Push(popup);
			currentPopup = popup;

			var builtPopup = popupBuilder.BuildPopup(popup, data.title, data.message,
				() => OnPopupClosed(),
				() => OnPopupClosed());

			if (isVRMode)
			{
				SetupVRPopup(builtPopup);
			}

			return builtPopup;
		}

private async UniTaskVoid ProcessQueue(CancellationToken ct, bool isHazard, float closeDuration = 0.2f)
{
	if (isProcessingQueue)
	{
		Debug.LogWarning("[PopupManager] ProcessQueue already running!");
		return;
	}

	Debug.Log("[PopupManager] ProcessQueue started");
	isProcessingQueue = true;

	try
	{
		while (popupQueue.Count > 0 && !ct.IsCancellationRequested)
		{
			Debug.Log($"[PopupManager] Processing popup. Queue count: {popupQueue.Count}");
			var request = popupQueue.Dequeue();

			try
			{
				await ShowPopupAsync(request, ct, isHazard, closeDuration);
			}
			catch (OperationCanceledException)
			{
				Debug.Log("[PopupManager] ShowPopup cancelled");
				throw;
			}
			
			// Delay'i her durumda yap (queue boş olsa bile, çünkü yeni item gelebilir)
			await UniTask.WaitForSeconds(delayBetweenPopups, cancellationToken: ct);
		}
	}
	catch (OperationCanceledException)
	{
		Debug.Log("[PopupManager] ProcessQueue cancelled");
	}
	finally
	{
		isProcessingQueue = false;
		Debug.Log($"[PopupManager] ProcessQueue finished. isProcessingQueue set to false. Remaining queue: {popupQueue.Count}");
	}
}

		private async UniTask ShowPopupAsync(PopupRequest request, CancellationToken ct, bool isHazard, float closeDuration)
		{
			BasePopup popup = factory.CreatePopup(popupPoolAdapter, isHazard);
			activePopups.Push(popup);
			currentPopup = popup;
			popup.SetCloseDuration(closeDuration);	

			var completionSource = new UniTaskCompletionSource();

			popupBuilder.BuildPopup(
				popup,
				request.data.title,
				request.data.message,
				() =>
				{
					completionSource.TrySetResult();
					OnPopupClosed();
				},
				() =>
				{
					completionSource.TrySetResult();
					OnPopupClosed();
				}
			);

			if (isVRMode)
			{
				SetupVRPopup(popup);
			}

			popup.Show();

			if (request.data.autoClose)
			{
				try
				{
					await UniTask.WaitForSeconds(
						request.data.displayDuration,
						ignoreTimeScale: true,
						cancellationToken: ct
					);

					popup.Hide();
					OnPopupClosed();
				}
				catch (OperationCanceledException)
				{
					Debug.Log("[PopupManager] ShowPopup cancelled during autoClose wait");
					throw;
				}
			}
			else
			{
				await completionSource.Task;
			}
		}

		private void SetupVRPopup(BasePopup popup)
		{
			if (vrCamera == null)
			{
				Debug.LogError("❌ Cannot setup VR popup - camera is null!");
				return;
			}

			Transform popupTransform = popup.transform;

			Canvas canvas = popup.GetComponentInParent<Canvas>();
			if (canvas != null)
			{
				if (canvas.renderMode != RenderMode.WorldSpace)
					canvas.renderMode = RenderMode.WorldSpace;

				canvas.worldCamera = vrCamera;
			}

			Vector3 toCam = vrCamera.transform.position - popupTransform.position;
			toCam.y = 0f;

			if (toCam.sqrMagnitude > 0.0001f)
				popupTransform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);

			RectTransform rectTransform = popup.GetComponent<RectTransform>();
			if (rectTransform != null)
				rectTransform.localScale = Vector3.one * vrPopupScale;
		}

		private void OnPopupClosed()
		{
			popupPoolAdapter.Return((currentPopup));
			currentPopup = null;
		}

		public void CloseCurrentPopup()
		{
			if (currentPopup != null)
			{
				Debug.Log("[PopupManager] Closing current popup");
				currentPopup.Hide();
				if (activePopups.Count > 0 && activePopups.Peek() == currentPopup)
				{
					activePopups.Pop();
				}

				popupPoolAdapter.Return((currentPopup));
				currentPopup = null;
			}
		}

		public void CloseTopPopup()
		{
			if (activePopups.Count > 0)
			{
				var popup = activePopups.Pop();
				popup.Hide();
			}
		}

		public void CloseAllPopups()
		{
			Debug.Log("[PopupManager] Closing all popups");
			popupQueue.Clear();

			while (activePopups.Count > 0)
			{
				CloseTopPopup();
			}

			currentPopup = null;
			isProcessingQueue = false; // Queue'yu temizlediğimizde flag'i de sıfırla
		}

		public void ClearQueue()
		{
			popupQueue.Clear();
		}

		public int GetQueueCount() => popupQueue.Count;

		private void OnSceneUnloaded(string _)
		{
			CancelAllTasks();
		}

		private void CancelAllTasks()
		{
			queueCts?.Cancel();
			queueCts?.Dispose();
			queueCts = new CancellationTokenSource();

			CloseAllPopups();
		}
	}

	public class PopupRequest
	{
		public PopupData data;
		public float timestamp;
	}
}