using UnityEngine;
using System;

public class LevelTimer : MonoBehaviour
{
	private float elapsed;
	private bool running;

	private void Start()
	{
		StartTimer();	
	}

	public void StartTimer()
	{
		elapsed = 0f;
		running = true;
	}

	public void StopTimer()
	{
		running = false;
	}

	public TimeSpan GetDuration()
	{
		return TimeSpan.FromSeconds(elapsed);
	}

	private void Update()
	{
		if (running)
		{
			elapsed += Time.deltaTime;
		}
	}
}
