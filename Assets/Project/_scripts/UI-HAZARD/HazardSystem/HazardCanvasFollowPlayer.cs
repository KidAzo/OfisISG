// using UnityEngine;
// using Woi.Events;

// public class HazardCanvasFollowPlayer : MonoBehaviour
// {
// 	[SerializeField] private Canvas canvas;

// 	[Header("Positioning Settings")]
// 	[SerializeField] private Vector3 localOffset = new Vector3(-25.22f, 0.29f, -19.70f);
// 	[SerializeField] private bool updateEveryFrame = true;
// 	[SerializeField] private bool matchCameraRotation = true;
// 	[SerializeField] private bool onlyYRotation = true; // Sadece Y ekseninde d�nd�r (daha do�al)

// 	[Header("Camera Reference")]
// 	private Transform vrCamera; // Inspector'dan atanabilir

// 	//private XRPlayerView playerView;

// 	private void Start()
// 	{
// 		//playerView = XRPlayerView.Instance;
// 		vrCamera = playerView.GetComponentInChildren<Camera>().transform;
// 	}

// 	private void OnEnable()
// 	{
// 		EventBus.Subscribe<OnHazardResult>(UpdatePosition_OnHazardResult);
// 	}

// 	private void OnDisable()
// 	{
// 		EventBus.Unsubscribe<OnHazardResult>(UpdatePosition_OnHazardResult);
// 	}

// 	private void LateUpdate()
// 	{
// 		if (updateEveryFrame && canvas.gameObject.activeInHierarchy)
// 		{
// 			PositionCanvasToCamera();
// 		}
// 	}

// 	public void PositionCanvasToCamera()
// 	{
// 		Vector3 worldPosition = vrCamera.TransformPoint(localOffset);
// 		canvas.transform.position = worldPosition;

// 		if (matchCameraRotation)
// 		{
// 			if (onlyYRotation)
// 			{
// 				Vector3 directionToCamera = vrCamera.position - canvas.transform.position;
// 				directionToCamera.y = 0; // Y bile�enini s�f�rla

// 				if (directionToCamera != Vector3.zero)
// 				{
// 					canvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
// 				}
// 			}
// 			else
// 			{
// 				canvas.transform.LookAt(vrCamera);
// 				canvas.transform.Rotate(0, 180, 0); // Canvas ters bakt��� i�in 180 derece d�nd�r
// 			}
// 		}
// 	}

// 	// Event tetiklendi�inde �a�r�l�r
// 	public void UpdatePosition_OnHazardResult(OnHazardResult evt)
// 	{
// 		PositionCanvasToCamera();
// 	}

// 	// Runtime'da offset de�i�tir
// 	public void SetLocalOffset(Vector3 newOffset)
// 	{
// 		localOffset = newOffset;
// 		PositionCanvasToCamera();
// 	}

// 	// Mevcut pozisyonu offset olarak kaydet (Debug i�in)
// 	[ContextMenu("Save Current Position as Offset")]
// 	public void SaveCurrentPositionAsOffset()
// 	{
// 		if (vrCamera != null && canvas != null)
// 		{
// 			localOffset = vrCamera.InverseTransformPoint(canvas.transform.position);
// 			Debug.Log($"Offset saved: {localOffset}");
// 		}
// 	}
// }