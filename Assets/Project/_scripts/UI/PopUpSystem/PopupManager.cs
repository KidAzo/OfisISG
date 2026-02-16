using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Events;

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

		PopupBuilder popupBuilder = new PopupBuilder();
		Stack<IPopup> activePopups = new Stack<IPopup>();
		Queue<PopupRequest> popupQueue = new Queue<PopupRequest>();
		Camera vrCamera;

		BasePopup currentPopup;
		bool isShowingPopup = false;
		CancellationTokenSource queueCts;
		bool vrCameraFound = false;

		void Start()
		{
			queueCts = new CancellationTokenSource();
			EventBus.Subscribe<OnSceneGroupLoaded>(OnSceneLoaded_Event);
			EventBus.Subscribe<OnSceneGroupUnloaded>(UnSeceneUnloaded_Event);
		}


		void LateUpdate()
		{
			// Kamera bulunamadıysa her frame dene
			if (!vrCameraFound && isVRMode)
			{
				FindVRCamera();
			}
		}

		void FindVRCamera()
		{
			try
			{
				// XRPlayerView'den kamerayı almaya çalış
				// if (XRPlayerView.Instance?.playerCamera != null)
				// {
				// 	vrCameraFound = true;
				// 	vrCamera = XRPlayerView.Instance.playerCamera;
				// 	Debug.Log("✅ VR Camera found for popup positioning");
				// }
			}
			catch (Exception e)
			{
				Debug.LogWarning($"⚠️ Error finding VR camera: {e.Message}");
			}
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
			vrCameraFound = false;
			vrCamera = null;
			FindVRCamera();
		}

		public void EnqueuePopup(PopupData data)
		{
			var request = new PopupRequest
			{
				data = data,
				timestamp = Time.time
			};

			popupQueue.Enqueue(request);

			if (!isShowingPopup)
			{
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
			while (popupQueue.Count > 0 && !ct.IsCancellationRequested)
			{
				isShowingPopup = true;
				var request = popupQueue.Dequeue();

				await ShowPopupAsync(request, ct, isHazard, closeDuration);
				await UniTask.WaitForSeconds(delayBetweenPopups, cancellationToken: ct);
			}

			isShowingPopup = false;
		}

		private async UniTask ShowPopupAsync(PopupRequest request, CancellationToken ct, bool isHazard, float closeDuration)
		{
			await WaitForVRCamera(ct);

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
				}
			}
			else
			{
				await completionSource.Task;
			}
		}

		// 👇 EKLE: Kamerayı bekle
		private async UniTask WaitForVRCamera(CancellationToken ct)
		{
			if (!isVRMode) return;

			int attempts = 0;
			int maxAttempts = 100; // 100 frame (yaklaşık 1-2 saniye)

			while (!vrCameraFound && attempts < maxAttempts && !ct.IsCancellationRequested)
			{
				FindVRCamera();

				if (!vrCameraFound)
				{
					await UniTask.Yield(ct); // Bir frame bekle
					attempts++;
				}
			}

			if (!vrCameraFound)
			{
				Debug.LogError($"❌ [PopupManager] VR Camera not found after {attempts} attempts!");
			}
			else
			{
				Debug.Log($"✅ [PopupManager] VR Camera ready after {attempts} attempts");
			}
		}

		// VR popup'ı için özel ayarlar
		private void SetupVRPopup(BasePopup popup)
		{
			if (vrCamera == null)
			{
				Debug.LogError("❌ Cannot setup VR popup - camera is null!");
				return;
			}

			Transform popupTransform = popup.transform;

			// Canvas'ı World Space'e çevir (eğer değilse)
			Canvas canvas = popup.GetComponentInParent<Canvas>();
			if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
			{
				canvas.renderMode = RenderMode.WorldSpace;
				canvas.worldCamera = vrCamera;
				Debug.Log("🖼️ Canvas set to WorldSpace");
			}

			// Pozisyonu ayarla
			Vector3 directionToCamera = vrCamera.transform.position - popupTransform.position;
			popupTransform.rotation = Quaternion.LookRotation(-directionToCamera);

			// Scale'i ayarla
			RectTransform rectTransform = popup.GetComponent<RectTransform>();
			if (rectTransform != null)
			{
				rectTransform.localScale = Vector3.one * vrPopupScale;
			}

			Debug.Log($"✅ VR Popup setup complete - Pos: {popupTransform.position}, Scale: {rectTransform?.localScale}");
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
			popupQueue.Clear();

			while (activePopups.Count > 0)
			{
				CloseTopPopup();
			}

			currentPopup = null;
			isShowingPopup = false;
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
