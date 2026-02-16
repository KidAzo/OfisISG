using UnityEngine;

public class VRCanvasFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform vrCamera;
    [SerializeField] private float distance = 2f;
    
    void LateUpdate()
    {
        transform.position = vrCamera.position + vrCamera.forward * distance;
        transform.LookAt(vrCamera);
        transform.Rotate(0, 180, 0); 
    }
}
