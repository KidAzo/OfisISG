using UnityEngine;

namespace WoiUtils.AudioSystem
{
    [CreateAssetMenu(fileName = "AudioSystemConfig", menuName = "WoiAudio/Audio System/Audio System Config", order = 1)]
    public class AudioSystemConfig : ScriptableObject
    {
        [Header("Global Settings")]
        public int defaultCapacity = 32;
        public int maxPoolSize = 100;
        public int maxSoundInstances = 64;
        public bool collectionCheck = false;
    }
}


