using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WoiUtils.AudioSystem
{
    // ======== Debug Snapshot Structs (Editor use) ========

    /// <summary>Lightweight snapshot of an active AudioVoice for editor debugging.</summary>
    public struct ActiveVoiceSnapshot
    {
        public string SoundName;
        public string ClipName;
        public bool IsPlaying;
        public bool IsLooping;
        public float SpatialBlend;
        public bool HasFollowTarget;
        public Vector3 Position;
        public bool IsActiveInHierarchy;
        public string DebugStatus;
    }

    /// <summary>Lightweight snapshot of queued sounds for editor debugging.</summary>
    public struct QueuedSoundSnapshot
    {
        public string SoundName;
        public int QueuedCount;
        public bool IsRunning;
        public SoundDefinition Sound;
    }

    public class AudioSystem : MonoBehaviour
    {
        [SerializeField] AudioSystemConfig config;

        public static bool IsShuttingDown { get; private set; } = false;

        AudioPoolAdapter adapter;

        // cooldown timestamps
        private readonly Dictionary<SoundDefinition, float> lastPlayTime = new();

        // SingleGlobal registry
        private readonly Dictionary<SoundDefinition, AudioVoice> singleGlobals = new();

        // clip selection state
        private readonly Dictionary<SoundDefinition, int> lastRandomIndex = new();
        private readonly Dictionary<SoundDefinition, int> sequenceIndex = new();

        // queue
        private readonly QueueController queue = new();

        // active voice tracking (for steal + debug)
        private readonly LinkedList<AudioVoice> activeOrder = new(); // oldest -> newest
        private readonly Dictionary<AudioVoice, LinkedListNode<AudioVoice>> activeNodes = new();

        public int ActiveVoiceCount => activeOrder.Count;

		public event System.Action<int> OnQueueIndexChanged {
			add { queue.OnClipChanged += value; }
			remove { queue.OnClipChanged -= value; }
		}

        // ---------------- Queue API ----------------
       // ---------------- Queue API ----------------
		public int GetQueueCount(SoundDefinition s) => queue.GetCount(s);

		public void ClearQueue() => queue.Clear(this);
		public void ClearQueue(SoundDefinition s) => queue.Clear(this, s);

		public bool SkipQueueOne(SoundDefinition s) => queue.DropOne(s);
		public bool QueueNext(SoundDefinition s) => queue.PlayNext(this, s);
		public bool QueuePrev(SoundDefinition s) => queue.PlayPrev(this, s);

		public (int currentIndex, int totalCount) GetQueuePosition(SoundDefinition s) => queue.GetQueuePosition(s);


        // ---------------- Debug Snapshot API (Editor) ----------------

        public List<ActiveVoiceSnapshot> GetActiveVoicesSnapshot()
        {
            var result = new List<ActiveVoiceSnapshot>(activeOrder.Count);

            var node = activeOrder.First;
            while (node != null)
            {
                var voice = node.Value;
                node = node.Next;

                bool isRefNull = ReferenceEquals(voice, null);
                bool isUnityNull = !isRefNull && voice == null;
                bool isActive = !isRefNull && !isUnityNull && voice.gameObject.activeInHierarchy;

                string status = "OK";
                if (isRefNull) status = "NULL_REF";
                else if (isUnityNull) status = "DESTROYED";
                else if (!isActive) status = "INACTIVE_GO";

                if (isRefNull || isUnityNull)
                {
                    result.Add(new ActiveVoiceSnapshot
                    {
                        SoundName = "(Invalid)",
                        ClipName = "-",
                        DebugStatus = status
                    });
                    continue;
                }

                var data = voice.Data;

                result.Add(new ActiveVoiceSnapshot
                {
                    SoundName = data != null ? data.name : "(null)",
                    ClipName = voice.GetCurrentClipName(),
                    IsPlaying = voice.IsPlaying(),
                    IsLooping = data != null && data.loop,
                    SpatialBlend = data != null ? data.spatialBlend : 0f,
                    HasFollowTarget = voice.HasFollowTarget(),
                    Position = voice.transform.position,
                    IsActiveInHierarchy = isActive,
                    DebugStatus = status
                });
            }

            return result;
        }

        public void SetPitchForActiveVoices(float pitch)
        {
            var node = activeOrder.First;
            while (node != null)
            {
                var v = node.Value;
                node = node.Next;

                if (v == null) continue;
                if (!v.isActiveAndEnabled) continue;

                v.SetPitch(pitch);
            }
        }

        public List<QueuedSoundSnapshot> GetQueuedSoundsSnapshot()
        {
            if (queue == null) return new List<QueuedSoundSnapshot>();
            return queue.GetQueuedSoundsSnapshot();
        }

        // ---------------- Unity lifecycle ----------------

        void OnApplicationQuit() => IsShuttingDown = true;

        void Awake()
        {
            IsShuttingDown = false;

            adapter = GetComponent<AudioPoolAdapter>();
            if (adapter == null)
                Debug.LogError("AudioSystem requires AudioPoolAdapter on the same GameObject.");
        }

        void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                IsShuttingDown = true;
                queue.Clear(this);
            }
            else
            {
                IsShuttingDown = false;
            }
        }

        void OnDestroy()
        {
            IsShuttingDown = true;

            queue.Clear(this);

            if (activeNodes != null && activeNodes.Count > 0)
            {
                var temp = new List<AudioVoice>(activeOrder);
                foreach (var v in temp)
                    if (v != null) v.Stop();
            }
        }

        // ---------------- Internal register/unregister ----------------

        bool RegisterVoice(AudioVoice voice)
        {
            if (voice == null) return false;

            if (activeNodes.ContainsKey(voice))
                return false;

            if (activeOrder.Count >= config.maxSoundInstances)
            {
                var oldest = FindStealCandidate();
                if (oldest != null) oldest.Stop();

                if (activeOrder.Count >= config.maxSoundInstances)
                    return false;
            }

            var node = activeOrder.AddLast(voice);
            activeNodes[voice] = node;
            return true;
        }

        void UnregisterVoice(AudioVoice voice)
        {
            if (voice == null) return;

            if (activeNodes.TryGetValue(voice, out var node))
            {
                activeOrder.Remove(node);
                activeNodes.Remove(voice);
            }
        }

        // ---------------- Public API ----------------

        public AudioVoice Play(SoundDefinition sound) => Play(sound, PlayContext.Default);

        // ✅ IMPORTANT: keep ctx (multipliers/clipIndex/ignoreCooldowns) by using the overloads below
        public AudioVoice PlayAt(SoundDefinition sound, Vector3 position) => PlayAt(sound, position, PlayContext.At(position));
        public AudioVoice PlayFollow(SoundDefinition sound, Transform follow) => PlayFollow(sound, follow, PlayContext.Follow(follow));

        public AudioVoice PlayAt(SoundDefinition sound, Vector3 position, in PlayContext baseCtx)
        {
            var ctx = baseCtx;
            ctx.hasPosition = true;
            ctx.position = position;
            ctx.hasFollow = false;
            ctx.follow = null;
            return Play(sound, ctx);
        }

        public AudioVoice PlayFollow(SoundDefinition sound, Transform follow, in PlayContext baseCtx)
        {
            var ctx = baseCtx;
            ctx.hasFollow = true;
            ctx.follow = follow;
            ctx.hasPosition = false;
            return Play(sound, ctx);
        }

        public void Enqueue(SoundDefinition sound) => Enqueue(sound, PlayContext.Default);

        public void Enqueue(SoundDefinition sound, in PlayContext ctx)
        {
            if (sound == null) return;
            queue.Enqueue(this, sound, ctx);
        }

        /// <summary>Enqueues ALL clips from the SoundDefinition to play sequentially.</summary>
        public void EnqueueAllClips(SoundDefinition sound) => EnqueueAllClips(sound, PlayContext.Default);

        public void EnqueueAllClips(SoundDefinition sound, in PlayContext ctx)
        {
                Debug.Log($"[EnqueueAllClips] {sound?.name} frame={Time.frameCount}\n{System.Environment.StackTrace}");

            if (sound == null || sound.clips == null) return;

            // prevent spam if requested
            if (sound.suppressDuplicatesWhileQueued && queue.IsRunning(sound))
                return;

            for (int i = 0; i < sound.clips.Count; i++)
            {
                var c = ctx;
                c = c.SetClipIndex(i);
                queue.Enqueue(this, sound, c);
            }
        }

        public void StopAll()
        {
            if (IsShuttingDown) return;

            var temp = new List<AudioVoice>(activeOrder);

            foreach (var v in temp)
                if (v != null)
                    v.Stop();

            activeOrder.Clear();
            activeNodes.Clear();
            singleGlobals.Clear();
            sequenceIndex.Clear();
            lastRandomIndex.Clear();

            queue.Clear(this);
        }

        public void StopAllInstances(SoundDefinition sound)
        {
            if (sound == null) return;

            var temp = new List<AudioVoice>(activeOrder);
            foreach (var v in temp)
            {
                if (v != null && v.Data == sound)
                    v.Stop();
            }
        }

        public void StopSingleGlobal(SoundDefinition sound)
        {
            if (sound == null) return;

            if (singleGlobals.TryGetValue(sound, out var voice) && voice != null)
                voice.Stop();

            singleGlobals.Remove(sound);
        }

        // --------------- Core play router ---------------

  public AudioVoice Play(SoundDefinition sound, in PlayContext ctx)
{
    if (sound == null) return null;

       if (sound.instanceMode == InstanceMode.SingleGlobal && IsAnyInstancePlaying(sound))
        return null;

    // 1. KARAR MEKANİZMASI: Bu ses kuyruğa (playlist) mi girmeli?
    // Hem QueueAll modu hem de Queue zamanlama modu burada kontrol edilir.
    bool isQueueRequest = (sound.selectionMode == ClipSelectionMode.QueueAll) || 
                          (!ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue);

    if (isQueueRequest && !ctx.hasClipIndex)
    {
        // EnqueueAllClips metodunu çağır ve işlemi burada bitir.
        // Bu metod kendi içinde 'GetCount' veya 'IsRunning' ile mükerrer kaydı engellemeli.
        EnqueueAllClips(sound, ctx);
        queue.PlayOrResume(this, sound);

        return null; 
    }

    // 2. ÖZEL DURUM: Kuyruktan gelen tekil klip çözünürlüğü
    if (!ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue && ctx.hasClipIndex)
    {
        queue.Enqueue(this, sound, ctx);
        return null;
    }

    // 3. NORMAL ÇALMA MANTIĞI (Immediate Play)
    // Önce klip seçimi yapılır.
    if (!TryResolveClipEntry(sound, ctx, out var clipEntry))
        return null;

    if (clipEntry.clip == null)
        return null;

    float totalDelay = 0f;

    // Cooldown ve Delay hesaplamaları
    if (!ctx.ignoreCooldowns)
    {
        totalDelay += ResolveSoundDelay(sound);
        totalDelay += Mathf.Max(0f, clipEntry.delay);
    }

    // Gecikmeli veya anında çalma
    if (totalDelay > 0f)
    {
        StartCoroutine(DelayedPlayResolved(sound, ctx, clipEntry.clip, totalDelay));
        return null;
    }

    return PlayImmediateResolved(sound, ctx, clipEntry.clip);
}

        internal float ResolveSoundDelay(SoundDefinition sound)
        {
            if (sound == null) return 0f;

            return sound.delayMode switch
            {
                DelayMode.Fixed => Mathf.Max(0f, sound.delay),
                DelayMode.RandomRange => Mathf.Max(0f, Random.Range(sound.delayRange.x, sound.delayRange.y)),
                _ => 0f
            };
        }

        IEnumerator DelayedPlayResolved(SoundDefinition sound, PlayContext ctx, AudioClip clip, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            PlayImmediateResolved(sound, ctx, clip);
        }

        // QueueController calls these:
        internal AudioVoice PlayImmediate(SoundDefinition sound, in PlayContext ctx)
        {
            if (adapter == null || sound == null) return null;

            if (!ctx.ignoreCooldowns && sound.cooldown > 0f)
            {
                float now = Time.unscaledTime;
                if (lastPlayTime.TryGetValue(sound, out float last))
                {
                    if ((now - last) < sound.cooldown)
                        return null;
                }
            }

            if (sound.instanceMode == InstanceMode.SingleGlobal)
            {
                if (singleGlobals.TryGetValue(sound, out var existing) && existing != null)
                {
                    if (!ctx.ignoreCooldowns && sound.reTriggerMode == ReTriggerMode.Ignore)
                        return existing;

                    existing.Stop();
                    singleGlobals.Remove(sound);
                }
            }

            if (!TryResolveClipEntry(sound, ctx, out var clipEntry))
                return null;

            var clip = clipEntry.clip;
            if (clip == null) return null;

            var voice = adapter.Get();
            voice.Bind(sound, this);
            voice.Apply(sound, clip, ctx);

            lastPlayTime[sound] = Time.unscaledTime;

            if (IsShuttingDown) { adapter.Return(voice); return null; }

            if (!RegisterVoice(voice))
            {
                adapter.Return(voice);
                return null;
            }

            voice.Play();

            if (sound.instanceMode == InstanceMode.SingleGlobal)
                singleGlobals[sound] = voice;

            return voice;
        }

        public AudioVoice PlayImmediateResolved(SoundDefinition sound, in PlayContext ctx, AudioClip clip)
        {
            if (adapter == null || sound == null || clip == null) return null;

            if (!ctx.ignoreCooldowns && sound.cooldown > 0f)
            {
                float now = Time.unscaledTime;
                if (lastPlayTime.TryGetValue(sound, out float last) && (now - last) < sound.cooldown)
                    return null;
            }

            if (sound.instanceMode == InstanceMode.SingleGlobal)
            {
                if (singleGlobals.TryGetValue(sound, out var existing) && existing != null)
                {
                    if (!ctx.ignoreCooldowns && sound.reTriggerMode == ReTriggerMode.Ignore)
                        return existing;

                    existing.Stop();
                    singleGlobals.Remove(sound);
                }
            }

            var voice = adapter.Get();
            voice.Bind(sound, this);
            voice.Apply(sound, clip, ctx);

            lastPlayTime[sound] = Time.unscaledTime;

            if (IsShuttingDown) { adapter.Return(voice); return null; }

            if (!RegisterVoice(voice))
            {
                adapter.Return(voice);
                return null;
            }

            voice.Play();

            if (sound.instanceMode == InstanceMode.SingleGlobal)
                singleGlobals[sound] = voice;

            return voice;
        }

        internal AudioVoice PlayImmediateFromQueue(SoundDefinition sound, in PlayContext ctx)
        {
            if (sound == null) return null;

            if (!TryResolveClipEntry(sound, ctx, out var entry) || entry.clip == null)
                return null;

            float totalDelay = ResolveSoundDelay(sound) + Mathf.Max(0f, entry.delay);

            if (totalDelay > 0f)
            {
                StartCoroutine(DelayedPlayResolved(sound, ctx, entry.clip, totalDelay));
                return null;
            }

            return PlayImmediateResolved(sound, ctx, entry.clip);
        }

        // ---------------- Clip Resolve ----------------

        public bool TryResolveClipEntry(SoundDefinition sound, in PlayContext ctx, out ClipEntry result)
        {
            result = default;

            if (sound == null || sound.clips == null || sound.clips.Count == 0)
                return false;

            if (ctx.hasClipIndex)
            {
                int i = ctx.clipIndex;
                if (i < 0 || i >= sound.clips.Count) return false;

                result = sound.clips[i];
                return result.clip != null;
            }

            switch (sound.selectionMode)
            {
                case ClipSelectionMode.Single:
                    result = sound.clips[0];
                    return result.clip != null;

                case ClipSelectionMode.Sequence:
                    return TryResolveSequence(sound, out result);

                case ClipSelectionMode.RandomWeighted:
                    return TryResolveRandomWeighted(sound, out result);

                default:
                    result = sound.clips[0];
                    return result.clip != null;
            }
        }

        bool TryResolveSequence(SoundDefinition sound, out ClipEntry result)
        {
            result = default;

            int count = sound.clips.Count;
            if (count == 0) return false;

            if (!sequenceIndex.TryGetValue(sound, out int idx))
                idx = 0;

            if (idx < 0 || idx >= count) idx = 0;

            result = sound.clips[idx];

            idx = (idx + 1) % count;
            sequenceIndex[sound] = idx;

            return result.clip != null;
        }

        bool TryResolveRandomWeighted(SoundDefinition sound, out ClipEntry result)
        {
            result = default;

            int count = sound.clips.Count;
            if (count == 0) return false;

            int chosen = ChooseWeightedIndex(sound);

            if (sound.noImmediateRepeat && count > 1)
            {
                if (lastRandomIndex.TryGetValue(sound, out int last) && chosen == last)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int retry = ChooseWeightedIndex(sound);
                        if (retry != last) { chosen = retry; break; }
                    }

                    if (chosen == last)
                        chosen = (last + 1) % count;
                }
            }

            lastRandomIndex[sound] = chosen;

            result = sound.clips[chosen];
            return result.clip != null;
        }

        int ChooseWeightedIndex(SoundDefinition sound)
        {
            int count = sound.clips.Count;

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += Mathf.Max(0f, sound.clips[i].weight);

            if (total <= 0f)
                return Random.Range(0, count);

            float r = Random.value * total;
            float acc = 0f;

            for (int i = 0; i < count; i++)
            {
                acc += Mathf.Max(0f, sound.clips[i].weight);
                if (r <= acc) return i;
            }

            return count - 1;
        }

        // ---------------- State queries ----------------

        public bool IsAnyInstancePlaying(SoundDefinition sound)
        {
            if (sound == null) return false;

            var node = activeOrder.First;
            while (node != null)
            {
                var v = node.Value;
                node = node.Next;

                if (v != null && v.Data == sound && v.IsPlaying())
                    return true;
            }
            return false;
        }

        // ---------------- Voice return ----------------

        internal void ReturnVoice(AudioVoice voice)
        {
            if (voice == null) return;

            UnregisterVoice(voice);

            var s = voice.Data;
            if (s != null && s.instanceMode == InstanceMode.SingleGlobal)
            {
                if (singleGlobals.TryGetValue(s, out var reg) && reg == voice)
                    singleGlobals.Remove(s);
            }

            if (IsShuttingDown) return;

            adapter.Return(voice);
        }

        AudioVoice FindStealCandidate()
        {
            var n = activeOrder.First;
            while (n != null)
            {
                var v = n.Value;
                var d = v != null ? v.Data : null;

                if (v != null && d != null && !d.protectedFromSteal)
                    return v;

                n = n.Next;
            }
            return null;
        }
    }
}
