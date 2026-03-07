using UnityEngine;

public class RotateYOnly : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 20f;

    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
    }
}