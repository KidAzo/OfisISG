using System;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace WOI.Modules.Audio
{
    /// <summary>
    /// Training / level flow entry point for one-shot <see cref="SoundDefinition"/> playback with completion callback.
    /// Default implementation: <see cref="WoiTrainingAudioManagerService"/> (registered at subsystem startup).
    /// </summary>
    public interface IAudioManagerService
    {
        /// <summary>Best-effort total seconds (clip lengths + entry delays) for logging / UI; not used for Queue All completion.</summary>
        float GetEstimatedDurationSeconds(SoundDefinition sound);

        /// <summary>Plays <paramref name="sound"/> via <see cref="AudioSystem"/>; invokes <paramref name="onComplete"/> when playback ends (including Queue All).</summary>
        void PlayWhenFinished(MonoBehaviour coroutineHost, SoundDefinition sound, Action onComplete);
    }
}
