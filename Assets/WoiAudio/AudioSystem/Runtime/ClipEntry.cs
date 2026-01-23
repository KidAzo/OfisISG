using System;
using UnityEngine;

namespace WoiUtils.AudioSystem
{
    [Serializable]
    public class ClipEntry
    {
        public AudioClip clip;
        [Min(0f)] public float weight = 1f;

        [Header("Timing")]
        public float delay;  // clip-specific delay
    }
}
