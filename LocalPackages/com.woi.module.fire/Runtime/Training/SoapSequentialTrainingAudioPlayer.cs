using System;
using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.Game.Training
{
    /// <summary>
    /// SOAP No Param dinleyip her tetikte ilgili <see cref="SoundDefinition"/>’ı
    /// <see cref="AudioSystem.EnqueueSequential"/> ile Woi Audio <b>list kuyruğuna</b> ekler (sırayla çalar).
    /// Bağlı her ses asset’inde: <see cref="ScheduleMode.Queue"/>, <see cref="QueueScope.PerCategory"/>,
    /// <see cref="InstanceMode.SingleGlobal"/> ve hepsi için aynı <see cref="SoundDefinition.category"/> veya aynı custom category key kullanın.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/SOAP Sequential Training Audio Player")]
    public sealed class SoapSequentialTrainingAudioPlayer : MonoBehaviour
    {
        [Serializable]
        public sealed class SoapSoundBinding
        {
            public ScriptableEventNoParam Event;
            public SoundDefinition Sound;
        }

        [Header("Audio")]
        [SerializeField]
        private AudioSystem _audioSystem;

        [Tooltip("Enqueue çağrılarında kullanılır (cooldown’ları atlamak için).")]
        [SerializeField]
        private bool _ignoreCooldowns = true;

        [Header("SOAP → Sound")]
        [Tooltip(
            "Her SoundDefinition: Scheduling = Queue, Instance = Single Global, Queue Scope = Per Category; " +
            "hepsi aynı Category (veya aynı Custom Category Key). Loop kapalı, genelde tek clip.")]
        [SerializeField]
        private List<SoapSoundBinding> _bindings = new List<SoapSoundBinding>();

        private readonly List<(ScriptableEventNoParam evt, Action handler)> _subscribed = new List<(ScriptableEventNoParam, Action)>();

        private void Awake()
        {
            if (_audioSystem == null && AudioSystem.TryGetFromServiceLocator(out var sys))
                _audioSystem = sys;

            if (_audioSystem == null)
                _audioSystem = FindFirstObjectByType<AudioSystem>();
        }

        private void OnEnable()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                SoapSoundBinding b = _bindings[i];
                if (b?.Event == null || b.Sound == null)
                    continue;

                ScriptableEventNoParam evt = b.Event;
                SoundDefinition snd = b.Sound;

                Action handler = () => PlayQueued(snd);
                evt.OnRaised += handler;
                _subscribed.Add((evt, handler));
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _subscribed.Count; i++)
            {
                (ScriptableEventNoParam evt, Action handler) = _subscribed[i];
                if (evt != null)
                    evt.OnRaised -= handler;
            }

            _subscribed.Clear();
        }

        private void PlayQueued(SoundDefinition sound)
        {
            if (_audioSystem == null || sound == null)
                return;

            // fireTrain scenes wire the same SOAP events to localized AudioTriggers (EN/TR) and keep legacy TR
            // SoundDefinition references here. Skip pair members so only the localized trigger plays.
            if (LocalizedSoundDefinition.ContainsSound(sound))
                return;

            var ctx = _ignoreCooldowns ? PlayContext.DebugNoCooldown() : PlayContext.Default;
            _audioSystem.EnqueueSequential(sound, ctx);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                SoundDefinition s = _bindings[i]?.Sound;
                if (s == null)
                    continue;

                if (s.scheduleMode != ScheduleMode.Queue)
                    Debug.LogWarning(
                        $"[{nameof(SoapSequentialTrainingAudioPlayer)}] '{s.name}': Scheduling Mode should be <b>Queue</b> so this clip uses the shared list-queue (sıralı çalma).",
                        this);

                if (s.instanceMode != InstanceMode.SingleGlobal)
                    Debug.LogWarning(
                        $"[{nameof(SoapSequentialTrainingAudioPlayer)}] '{s.name}': Instance Mode should be <b>Single Global</b> for shared Per Category queue.",
                        this);

                if (s.queueScope != QueueScope.PerCategory)
                    Debug.LogWarning(
                        $"[{nameof(SoapSequentialTrainingAudioPlayer)}] '{s.name}': Queue Scope should be <b>Per Category</b> so different sounds share one queue.",
                        this);
            }
        }
#endif
    }
}
