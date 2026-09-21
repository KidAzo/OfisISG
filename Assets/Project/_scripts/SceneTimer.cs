using System;
using UnityEngine;

public class SceneTimer : MonoBehaviour
{
    private float sceneStartTime;

    void Start()
    {
        sceneStartTime = Time.time;
    }

    public TimeSpan GetElapsedTime()
    {
        return TimeSpan.FromSeconds(Time.time - sceneStartTime);
    }
}
