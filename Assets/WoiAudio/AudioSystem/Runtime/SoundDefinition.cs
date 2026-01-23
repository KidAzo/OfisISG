using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace WoiUtils.AudioSystem
{
    [CreateAssetMenu(fileName = "Sound Data", menuName = "SO/Audio/SoundData", order = 0)]
    public class SoundDefinition : ScriptableObject
    {
        [Header("Clips")]
        public ClipSelectionMode selectionMode = ClipSelectionMode.Single;
        public List<ClipEntry> clips = new(); // if clips.Count == 0 quiet 
        public bool noImmediateRepeat = true;
        
        [Header("Category & Routing")]
        public AudioCategory category = AudioCategory.SFX;
        public bool useCustomCategory = false;
        public string customCategoryKey = "";
        public AudioMixerGroup mixerGroup;

        [Header("Scheduling")]
        public ScheduleMode scheduleMode = ScheduleMode.Immediate;
        public QueueScope queueScope = QueueScope.PerSound;

        [Header("Instance")]
        public InstanceMode instanceMode = InstanceMode.Multiple;
        public ReTriggerMode reTriggerMode = ReTriggerMode.Restart;

        [Header("Timing")]
        public float cooldown = 0f;
        public DelayMode delayMode = DelayMode.None;
        public float delay = 0f;
        public Vector2 delayRange = Vector2.zero;

        [Header("Playback")]
        public bool loop;
        [Range(0, 256)] public int priority = 128;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(-3f, 3f)] public float pitch = 1f;

        [Header("3D")]
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
        public float minDistance = 1f;
        public float maxDistance = 500f;
        public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Advanced (optional)")]
        public bool mute;
        public bool bypassEffects;
        public bool bypassListenerEffects;
        public bool bypassReverbZones;
        public float panStereo;
        public float reverbZoneMix = 1f;
        public float dopplerLevel = 1f;
        public float spread;
        public bool ignoreListenerVolume;
        public bool ignoreListenerPause;
        public bool protectedFromSteal;    
        public bool suppressDuplicatesWhileQueued = true;
        }
}
