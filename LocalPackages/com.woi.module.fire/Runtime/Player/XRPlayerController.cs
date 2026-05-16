using UnityEngine;

public class XRPlayerController : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key was pressed first time");
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key was pressed second time");
        }
    }
}
