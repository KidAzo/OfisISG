using UnityEngine;
using Woi.Game.VFX;
using WoiUtils.AudioSystem;

namespace Woi.Game.Training
{
    /// <summary>
    /// Stops gameplay audio before the training results UI opens (Woi AudioSystem, loop drivers, Unity AudioSources).
    /// </summary>
    public static class TrainingGameplayAudioSilencer
    {
        public static void StopAllSceneGameplayAudio()
        {
            StopAllAudioSystems();
            StopLoopDriverComponents();
            StopUnityAudioSources();
        }

        static void StopAllAudioSystems()
        {
            AudioSystem[] systems = Object.FindObjectsByType<AudioSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < systems.Length; i++)
            {
                AudioSystem sys = systems[i];
                if (sys != null)
                    sys.StopAll();
            }
        }

        static void StopLoopDriverComponents()
        {
            FireSourceIntensityLoopAudio[] fireLoops = Object.FindObjectsByType<FireSourceIntensityLoopAudio>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < fireLoops.Length; i++)
            {
                if (fireLoops[i] != null)
                    fireLoops[i].Stop();
            }

            ExtinguisherSprayLoopAudio[] sprayLoops = Object.FindObjectsByType<ExtinguisherSprayLoopAudio>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sprayLoops.Length; i++)
            {
                if (sprayLoops[i] != null)
                    sprayLoops[i].ForceStopGameplayAudio();
            }
        }

        static void StopUnityAudioSources()
        {
            AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source != null && source.isPlaying)
                    source.Stop();
            }
        }
    }
}
