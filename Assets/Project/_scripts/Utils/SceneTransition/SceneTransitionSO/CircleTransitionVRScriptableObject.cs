using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace WoiUtils.SceneTransition
{
	[CreateAssetMenu(fileName = "Circle_VR", menuName = "SO/Scene Transitions/Circle_VR")]
	public class CircleTransitionVRScriptableObject : SceneTransitionSO
	{
		public Sprite CircleSprite;
		public Color Color;

		public override async UniTask Enter(Canvas parent)
		{
			var worldCanvas = CreateWorldSpaceCanvas();
			AnimatedObject = CreateImage(worldCanvas);
			AnimatedObject.color = Color;
			AnimatedObject.sprite = CircleSprite;

			float size = 5f;
			float time = 0;
			Vector2 initialSize = new Vector2(size, size);
			while (time < 1f)
			{
				AnimatedObject.rectTransform.sizeDelta = Vector2.Lerp(
					initialSize,
					Vector2.zero,
					LerpCurve.Evaluate(time)
				);

				await UniTask.Yield();
				time += Time.deltaTime / AnimationTime;
			}

			Destroy(worldCanvas.gameObject);
		}

		public override async UniTask Exit(Canvas parent)
		{
			var worldCanvas = CreateWorldSpaceCanvas();
			AnimatedObject = CreateImage(worldCanvas);
			AnimatedObject.color = Color;
			AnimatedObject.sprite = CircleSprite;
			AnimatedObject.rectTransform.sizeDelta = Vector2.zero;

			float size = 5f;
			float time = 0;
			Vector2 targetSize = new Vector2(size, size);
			while (time < 1f)
			{
				AnimatedObject.rectTransform.sizeDelta = Vector2.Lerp(
					Vector2.zero,
					targetSize,
					LerpCurve.Evaluate(time)
				);

				await UniTask.Yield();
				time += Time.deltaTime / AnimationTime;
			}
		}

		private Canvas CreateWorldSpaceCanvas()
		{
			GameObject canvasGO = new GameObject("VR_TransitionCanvas");
			Canvas canvas = canvasGO.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.worldCamera = Camera.main;

			CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
			scaler.dynamicPixelsPerUnit = 10;

			RectTransform rectTransform = canvas.GetComponent<RectTransform>();
			rectTransform.sizeDelta = new Vector2(2, 2);

			canvasGO.AddComponent<GraphicRaycaster>();

			PositionCanvasToCamera(canvas);

			return canvas;
		}

		private void PositionCanvasToCamera(Canvas canvas)
		{
			Transform cam = Camera.main.transform;
			float distance = 1.2f; 
			float heightOffset = 0.0f; 

			Vector3 targetPosition = cam.position + cam.forward * distance + cam.up * heightOffset;
			canvas.transform.position = targetPosition;

			// DÜZELTME BURADA: Canvas kameraya doğru dönsün
			canvas.transform.rotation = Quaternion.LookRotation(targetPosition - cam.position);
		}
	}
}
