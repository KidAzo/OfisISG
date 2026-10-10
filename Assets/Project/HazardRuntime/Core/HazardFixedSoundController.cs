using System.Threading;
using UnityEngine;
using Woi.Events;
using WoiUtils;
using WoiUtils.AudioSystem;

public class HazardFixedSoundController : Singleton<HazardFixedSoundController>
{
	//[Inject] SoundManager soundManager;

	private CancellationTokenSource soundCts;

	protected override void Awake()
	{
		base.Awake();
		soundCts = new CancellationTokenSource();
	}

	// public void OnEnable()
	// {
	// 	EventBus.Subscribe<OnLevelLoadingAllComplate>(ClearAllQueuedSounds);
	// }

	// private void OnDisable()
	// {
	// 	EventBus.Unsubscribe<OnLevelLoadingAllComplate>(ClearAllQueuedSounds);
	// }

	private void OnDestroy()
	{
		soundCts?.Cancel();
		soundCts?.Dispose();
	}

	// public static void PlaySound(SoundData data)
	// {
	// 	Instance.soundManager.PlaySoundQueued(data);
	// }


	// public void SkipCurrentSound()
	// {
	// 	soundManager.SkipQueuedSound();
	// }

	// public void ClearAllQueuedSounds(OnLevelLoadingAllComplate evt)
	// {
	// 	soundManager.ClearSoundQueue();

	// 	// Refresh token
	// 	soundCts?.Cancel();
	// 	soundCts?.Dispose();
	// 	soundCts = new CancellationTokenSource();
	// }
}