using UnityEngine;

public class FishSwim : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;

    public float speed = 1.2f;

    [Header("Wave Motion")]
    public float waveAmplitude = 0.15f;
    public float waveFrequency = 2f;

    [Header("Rotation")]
    public float turnSpeed = 3f;

    Transform target;
    Vector3 basePosition;
    float waveOffset;

    void Start()
    {
        target = pointB;
        waveOffset = Random.Range(0f, 100f); // balýklar senkron yüzmesin
    }

    void Update()
    {
        Move();
        Rotate();
        CheckTarget();
    }

    void Move()
    {
        basePosition = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime);

        Vector3 dir = (target.position - transform.position).normalized;

        // saða doðru lateral wave
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        float wave = Mathf.Sin(Time.time * waveFrequency + waveOffset) * waveAmplitude;

        transform.position = basePosition + side * wave;
    }

    void Rotate()
    {
        Vector3 direction = target.position - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime);
        }
    }

    void CheckTarget()
    {
        if (Vector3.Distance(transform.position, target.position) < 0.2f)
        {
            target = target == pointA ? pointB : pointA;
        }
    }
}