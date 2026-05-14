using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WOI.Modules.SDK;

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

    [DefaultExecutionOrder(-8000)]
    public class AudioSystem : MonoBehaviour
    {
        [SerializeField] AudioSystemConfig config;

        [Header("Service locator")]
        [Tooltip("Registers this instance on ServiceLocator in Awake (early) so AudioTrigger/UI can resolve before Start.")]
        [SerializeField]
        private bool registerWithServiceLocator = true;

        private bool _registeredWithServiceLocator;

        /// <summary>The single persisted runtime instance after <see cref="DontDestroyOnLoad"/> (handles duplicate scene loads).</summary>
        private static AudioSystem _persistedInstance;

        public static bool IsShuttingDown { get; private set; } = false;

        /// <summary>
        /// Returns the <see cref="AudioSystem"/> registered on <see cref="ServiceLocator"/> (registration runs in <see cref="Awake"/>).
        /// </summary>
        public static bool TryGetFromServiceLocator(out AudioSystem system) =>
            ServiceLocator.TryGet<AudioSystem>(out system);

        AudioPoolAdapter adapter;

        /// <summary>
        /// Added only while no other enabled listener exists in the hierarchy (common gap during additive scene loads).
        /// Removed when a scene listener (e.g. camera) becomes active again.
        /// </summary>
        AudioListener _fallbackAudioListener;

        // cooldown timestamps
        private readonly Dictionary<SoundDefinition, float> lastPlayTime = new();

        // SingleGlobal registry
        private readonly Dictionary<SoundDefinition, AudioVoice> singleGlobals = new();

        // clip selection state
        private readonly Dictionary<SoundDefinition, int> lastRandomIndex = new();
        private readonly Dictionary<SoundDefinition, int> sequenceIndex = new();
        

        // queue
        private readonly QueueController queue = new();

        /// <summary>Play() / PlayImmediateFromQueue() delayed starts — must be cancelled when <see cref="StopAllInstances"/> runs before the clip begins.</summary>
        private readonly Dictionary<int, Coroutine> _pendingDelayedPlayBySoundId = new();

        // active voice tracking (for steal + debug)
        private readonly LinkedList<AudioVoice> activeOrder = new(); // oldest -> newest
        private readonly Dictionary<AudioVoice, LinkedListNode<AudioVoice>> activeNodes = new();

        public int ActiveVoiceCount => activeOrder.Count;

        /// <summary>Fired when a queue starts an item. Args: owning <see cref="SoundDefinition"/>, clip slot index on that sound.</summary>
        public event System.Action<SoundDefinition, int> OnQueueIndexChanged
        {
            add { queue.OnClipChanged += value; }
            remove { queue.OnClipChanged -= value; }
        }

        // ---------------- Queue API ----------------
        // ---------------- Queue API ----------------
        public int GetQueueCount(SoundDefinition s) => queue.GetCount(s);

        public void ClearQueue()
        {
            CancelAllPendingDelayedPlays();
            queue.Clear(this);
        }
        public void ClearQueue(SoundDefinition s)
        {
            CancelPendingDelayedPlayForSound(s);
            queue.Clear(this, s);
        }

        public bool SkipQueueOne(SoundDefinition s) => queue.DropOne(s);
        public bool QueueNext(SoundDefinition s) => queue.PlayNext(this, s);
        public bool QueuePrev(SoundDefinition s) => queue.PlayPrev(this, s);

        public (int currentIndex, int totalCount) GetQueuePosition(SoundDefinition s) => queue.GetQueuePosition(s);

        /// <summary>
        /// True while the internal queue runner coroutine is processing this sound (e.g. <see cref="ClipSelectionMode.QueueAll"/>).
        /// Use this instead of estimating clip durations when <see cref="Play"/> returns null.
        /// </summary>
        public bool IsQueueRunnerActive(SoundDefinition s) => s != null && queue.IsRunning(s);


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

            if (_persistedInstance != null && !ReferenceEquals(_persistedInstance, this))
            {
                Destroy(gameObject);
                return;
            }

            _persistedInstance = this;
            DontDestroyOnLoad(gameObject);

            adapter = GetComponent<AudioPoolAdapter>();
            if (adapter == null)
            {
                Debug.LogError("AudioSystem requires AudioPoolAdapter on the same GameObject.");
                return;
            }

            TryRegisterWithServiceLocator();
        }

        void TryRegisterWithServiceLocator()
        {
            if (!registerWithServiceLocator)
                return;

            if (_registeredWithServiceLocator)
                return;

            if (adapter == null)
                return;

            if (ServiceLocator.TryGet<AudioSystem>(out AudioSystem existing) && existing != null)
            {
                if (ReferenceEquals(existing, this))
                {
                    _registeredWithServiceLocator = true;
                    return;
                }

                Debug.LogWarning("[AudioSystem] ServiceLocator already has an AudioSystem — this instance was not registered.", this);
                return;
            }

            ServiceLocator.Register<AudioSystem>(this);
            _registeredWithServiceLocator = true;
        }

        void TryUnregisterWithServiceLocator()
        {
            if (!_registeredWithServiceLocator)
                return;

            ServiceLocator.Unregister<AudioSystem>();
            _registeredWithServiceLocator = false;
        }

        void OnApplicationPause(bool pause)
        {
            // Do not set IsShuttingDown here — on some platforms pause can fire during loads; leaving it true blocks all Play() until an unpause that never comes.
            if (pause)
            {
                CancelAllPendingDelayedPlays();
                queue.Clear(this);
            }
        }

        void OnDestroy()
        {
            if (ReferenceEquals(_persistedInstance, this))
                _persistedInstance = null;

            TryUnregisterWithServiceLocator();

            IsShuttingDown = true;

            CancelAllPendingDelayedPlays();
            queue.Clear(this);

            if (activeNodes != null && activeNodes.Count > 0)
            {
                var temp = new List<AudioVoice>(activeOrder);
                foreach (var v in temp)
                    if (v != null) v.Stop();
            }
        }

        // ---------------- Internal register/unregister ----------------

        bool RegisterVoice(AudioVoice voice, in PlayContext ctx)
        {
            if (voice == null) return false;

            if (activeNodes.ContainsKey(voice))
                return false;

            if (activeOrder.Count >= config.maxSoundInstances)
            {
                AudioVoice oldest = null;
                if (ctx.suppressSameCategorySteal && voice.Data != null)
                {
                    // Only recycle voices in the same *custom* category bucket. Broad enum categories (e.g. all SFX)
                    // would otherwise steal unrelated gameplay sounds. If this sound has no custom key, skip stealing.
                    if (voice.Data.useCustomCategory)
                        oldest = FindStealCandidateMatchingCategory(voice.Data);
                }
                else
                {
                    oldest = FindStealCandidate();
                }

                if (oldest != null)
                    oldest.Stop();

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

        /// <summary>Same mixer/category routing as <see cref="SoundDefinition.category"/> / custom key.</summary>
        static bool CategoriesMatch(SoundDefinition a, SoundDefinition b)
        {
            if (a == null || b == null)
                return false;

            if (a.useCustomCategory != b.useCustomCategory)
                return false;

            if (a.useCustomCategory)
                return string.Equals(a.customCategoryKey ?? "", b.customCategoryKey ?? "", System.StringComparison.Ordinal);

            return a.category == b.category;
        }

        /// <summary>For <see cref="InstanceMode.SinglePerCategory"/> — stops other clips (any SoundDefinition) in the same category bucket.</summary>
        void StopActiveVoicesInMatchingCategory(SoundDefinition incoming, in PlayContext ctx)
        {
            if (incoming == null || incoming.instanceMode != InstanceMode.SinglePerCategory)
                return;

            if (ctx.suppressSameCategorySteal)
                return;

            var snapshot = new List<AudioVoice>(activeOrder);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var voice = snapshot[i];
                if (voice == null)
                    continue;

                var data = voice.Data;
                if (data == null || !CategoriesMatch(incoming, data))
                    continue;

                voice.Stop();
            }
        }

        // ---------------- Public API ----------------

        public AudioVoice Play(SoundDefinition sound) => Play(sound, PlayContext.Default);

        // ✅ IMPORTANT: keep ctx (multipliers/clipIndex/ignoreCooldowns) by using the overloads below
        public AudioVoice PlayAt(SoundDefinition sound, Vector3 position) => PlayAt(sound, position, PlayContext.At(position));
        public AudioVoice PlayFollow(SoundDefinition sound, Transform follow) => PlayFollow(sound, follow, PlayContext.Follow(follow));
        public void PlayOrResumeQueue(SoundDefinition s) => queue.PlayOrResume(this, s);

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

        /// <summary>
        /// Enqueues the sound’s default clip (slot 0) and starts the queue runner if idle.
        /// For cross-sound sequential VO, use <see cref="InstanceMode.SingleGlobal"/> + same <see cref="QueueScope"/> / category on all involved <see cref="SoundDefinition"/> assets
        /// (typically <see cref="QueueScope.PerCategory"/> + shared <see cref="SoundDefinition.customCategoryKey"/>), then call this from each SOAP handler instead of <see cref="Play"/>.
        /// </summary>
        public void EnqueueSequential(SoundDefinition sound, in PlayContext ctx)
        {
            if (sound == null)
                return;

            queue.Enqueue(this, sound, ctx);
            queue.NotifyEnqueueCompleted(this, sound);
        }

        /// <summary>Enqueues ALL clips from the SoundDefinition to play sequentially.</summary>
        public void EnqueueAllClips(SoundDefinition sound) => EnqueueAllClips(sound, PlayContext.Default);

        public void EnqueueAllClips(SoundDefinition sound, in PlayContext ctx)
        {
            if (sound == null || sound.clips == null) return;

            // prevent spam if requested
            // Only suppress duplicates for SingleGlobal (resume use-case)
            if (sound.suppressDuplicatesWhileQueued &&
                sound.instanceMode == InstanceMode.SingleGlobal &&
                queue.IsRunning(sound))
            {
                return;
            }

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

            CancelAllPendingDelayedPlays();

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

            CancelPendingDelayedPlayForSound(sound);

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

        /// <summary>
        /// Unity plays nothing when no <see cref="AudioListener"/> is active. Scene unloading often removes the only listener before the next scene’s camera is ready.
        /// </summary>
        void EnsureActiveAudioListenerPresent()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            AudioListener firstActive = null;

            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy)
                    continue;

                firstActive = l;
                break;
            }

            if (firstActive != null)
            {
                if (_fallbackAudioListener != null && firstActive != _fallbackAudioListener)
                {
                    Destroy(_fallbackAudioListener);
                    _fallbackAudioListener = null;
                }

                return;
            }

            if (_fallbackAudioListener == null)
                _fallbackAudioListener = gameObject.AddComponent<AudioListener>();
        }

        // --------------- Core play router ---------------

        public AudioVoice Play(SoundDefinition sound, in PlayContext ctx)
        {
            if (sound == null) return null;

            // 1. DECISION LOGIC: Should this sound be added to the queue (playlist)?
            // Both QueueAll mode and Queue scheduling mode are checked here.
            bool isQueueRequest = !ctx.forceImmediatePlay &&
                                  ((sound.selectionMode == ClipSelectionMode.QueueAll) ||
                                   (!ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue));

            if (isQueueRequest && !ctx.hasClipIndex)
            {
                // Call EnqueueAllClips and finish the operation here.
                // This method should prevent duplicate entries using 'GetCount' or 'IsRunning'.
                queue.PlayOrResume(this, sound);

                return null;
            }

            // 2. SPECIAL CASE: Single clip resolution from queue
            if (!ctx.forceImmediatePlay && !ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue && ctx.hasClipIndex)
            {
                queue.Enqueue(this, sound, ctx);
                return null;
            }

            // 3. NORMAL PLAY LOGIC (Immediate Play)
            // First, clip selection is performed.
            if (!TryResolveClipEntry(sound, ctx, out var clipEntry))
                return null;

            if (clipEntry.clip == null)
                return null;

            float totalDelay = 0f;

            // Cooldown and Delay calculations
            if (!ctx.ignoreCooldowns)
            {
                totalDelay += ResolveSoundDelay(sound);
                totalDelay += Mathf.Max(0f, clipEntry.delay);
            }

            // Delayed or immediate playback
            if (totalDelay > 0f)
            {
                StartTrackedDelayedPlay(sound, ctx, clipEntry.clip, totalDelay);
                return null;
            }

            return PlayImmediateResolved(sound, ctx, clipEntry.clip);
        }

        void CancelPendingDelayedPlayForSound(SoundDefinition sound)
        {
            if (sound == null)
                return;

            int id = sound.GetInstanceID();
            if (_pendingDelayedPlayBySoundId.TryGetValue(id, out Coroutine co) && co != null)
                StopCoroutine(co);

            _pendingDelayedPlayBySoundId.Remove(id);
        }

        void CancelAllPendingDelayedPlays()
        {
            foreach (KeyValuePair<int, Coroutine> kv in _pendingDelayedPlayBySoundId)
            {
                if (kv.Value != null)
                    StopCoroutine(kv.Value);
            }

            _pendingDelayedPlayBySoundId.Clear();
        }

        void StartTrackedDelayedPlay(SoundDefinition sound, PlayContext ctx, AudioClip clip, float delay)
        {
            if (sound == null || clip == null)
                return;

            int soundId = sound.GetInstanceID();
            CancelPendingDelayedPlayForSound(sound);

            Coroutine co = null;

            IEnumerator Run()
            {
                try
                {
                    if (delay > 0f)
                        yield return new WaitForSecondsRealtime(delay);

                    if (IsShuttingDown)
                        yield break;

                    if (!_pendingDelayedPlayBySoundId.TryGetValue(soundId, out Coroutine reg) || !ReferenceEquals(reg, co))
                        yield break;

                    PlayImmediateResolved(sound, ctx, clip);
                }
                finally
                {
                    if (_pendingDelayedPlayBySoundId.TryGetValue(soundId, out Coroutine reg) && ReferenceEquals(reg, co))
                        _pendingDelayedPlayBySoundId.Remove(soundId);
                }
            }

            co = StartCoroutine(Run());
            _pendingDelayedPlayBySoundId[soundId] = co;
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

            StopActiveVoicesInMatchingCategory(sound, in ctx);

            if (sound.instanceMode == InstanceMode.SingleGlobal)
            {
                if (singleGlobals.TryGetValue(sound, out var existing) && existing != null)
                {
                    if (!existing.IsPlaying())
                    {
                        singleGlobals.Remove(sound);
                    }
                    else
                    {
                        if (!ctx.ignoreCooldowns && sound.reTriggerMode == ReTriggerMode.Ignore)
                            return existing;

                        existing.Stop();
                        singleGlobals.Remove(sound);
                    }
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

            if (!RegisterVoice(voice, in ctx))
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
            EnsureActiveAudioListenerPresent();

            if (adapter == null || sound == null || clip == null) return null;

            if (!ctx.ignoreCooldowns && sound.cooldown > 0f)
            {
                float now = Time.unscaledTime;
                if (lastPlayTime.TryGetValue(sound, out float last) && (now - last) < sound.cooldown)
                    return null;
            }

            StopActiveVoicesInMatchingCategory(sound, in ctx);

            if (sound.instanceMode == InstanceMode.SingleGlobal)
            {
                if (singleGlobals.TryGetValue(sound, out var existing) && existing != null)
                {
                    // stale: if no longer playing, clean up registry
                    if (!existing.IsPlaying())
                    {
                        singleGlobals.Remove(sound);
                    }
                    else
                    {
                        // what to do if still playing?
                        if (!ctx.ignoreCooldowns && sound.reTriggerMode == ReTriggerMode.Ignore)
                            return existing;

                        existing.Stop();
                        singleGlobals.Remove(sound);
                    }
                }
            }


            var voice = adapter.Get();
            voice.Bind(sound, this);
            voice.Apply(sound, clip, ctx);

            lastPlayTime[sound] = Time.unscaledTime;

            if (IsShuttingDown) { adapter.Return(voice); return null; }

            if (!RegisterVoice(voice, in ctx))
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
                StartTrackedDelayedPlay(sound, ctx, entry.clip, totalDelay);
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

        /// <summary>
        /// Oldest stealable voice whose category bucket matches <paramref name="incoming"/> (same rules as <see cref="CategoriesMatch"/>).
        /// </summary>
        AudioVoice FindStealCandidateMatchingCategory(SoundDefinition incoming)
        {
            if (incoming == null)
                return null;

            for (LinkedListNode<AudioVoice> n = activeOrder.First; n != null; n = n.Next)
            {
                AudioVoice v = n.Value;
                SoundDefinition d = v != null ? v.Data : null;

                if (v != null && d != null && !d.protectedFromSteal && CategoriesMatch(incoming, d))
                    return v;
            }

            return null;
        }
    }
}
