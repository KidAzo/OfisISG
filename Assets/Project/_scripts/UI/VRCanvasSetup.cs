using Reflex.Attributes;
using UnityEngine;
using Woi.Player;

public class VRCanvasSetup : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] Camera vrCamera;
    [SerializeField] RenderMode renderMode = RenderMode.WorldSpace;
    [SerializeField] float retryInterval = 0.1f;
    [SerializeField] int maxRetries = 500;
    [Inject] IXRPlayerService xrPlayerService;
    bool isAssigned = false;

   void Start()
    {
            canvas.renderMode = renderMode;
            canvas.worldCamera = vrCamera;
            canvas.planeDistance = 0.1f;
            
            this.enabled = false; 
    }

    void LateUpdate()
    {
            canvas.worldCamera = vrCamera;
            canvas.planeDistance = 0.1f;
            
            isAssigned = true;
            this.enabled = false; 
    }
}
