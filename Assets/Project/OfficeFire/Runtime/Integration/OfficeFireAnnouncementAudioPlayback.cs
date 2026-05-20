using UnityEngine;
using Woi.UI.Announcements;
using WoiUtils.AudioSystem;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Shared Woi Audio playback for Office Fire announcement presenters.
    /// </summary>
    public static class OfficeFireAnnouncementAudioPlayback
    {
        public static float EstimateDuration(LocalizedSoundDefinition localizedSound)
        {
            if (localizedSound == null)
            {
                return 0f;
            }

            SoundDefinition sound = localizedSound.ResolveForCurrentLanguage();
            return WoiAnnouncementAudioAdapter.EstimatePlaybackDuration(sound);
        }

        public static void Play(WoiAnnouncementAudioAdapter adapter, LocalizedSoundDefinition localizedSound)
        {
            if (localizedSound == null)
            {
                return;
            }

            SoundDefinition sound = localizedSound.ResolveForCurrentLanguage();
            if (sound == null)
            {
                Debug.LogWarning("[OfficeFire] LocalizedSoundDefinition resolved to null for current language.");
                return;
            }

            WoiAnnouncementAudioAdapter resolvedAdapter = ResolveAdapter(adapter);
            if (resolvedAdapter == null)
            {
                Debug.LogWarning("[OfficeFire] WoiAnnouncementAudioAdapter not found — voice skipped.");
                return;
            }

            resolvedAdapter.PlayAnnouncement(sound);
        }

        public static void Stop(WoiAnnouncementAudioAdapter adapter)
        {
            WoiAnnouncementAudioAdapter resolvedAdapter = ResolveAdapter(adapter);
            if (resolvedAdapter == null)
            {
                return;
            }

            resolvedAdapter.StopCurrentAnnouncement();
        }

        public static WoiAnnouncementAudioAdapter ResolveAdapter(WoiAnnouncementAudioAdapter adapter)
        {
            if (adapter != null)
            {
                return adapter;
            }

            return Object.FindFirstObjectByType<WoiAnnouncementAudioAdapter>();
        }

        public static WoiAnnouncementAudioAdapter EnsureAdapter(GameObject host, WoiAnnouncementAudioAdapter adapter)
        {
            if (adapter != null)
            {
                return adapter;
            }

            if (host == null)
            {
                return null;
            }

            adapter = host.GetComponent<WoiAnnouncementAudioAdapter>();
            if (adapter != null)
            {
                return adapter;
            }

            return host.AddComponent<WoiAnnouncementAudioAdapter>();
        }
    }
}
