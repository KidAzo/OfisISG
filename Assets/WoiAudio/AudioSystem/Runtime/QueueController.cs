using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoiUtils.AudioSystem
{
    /// <summary>
    /// List + currentIndex queue controller (supports Next/Prev + autoplay).
    ///
    /// Goals:
    /// - Autoplay (Play button): plays sequentially from index 0 (or resumes if SingleGlobal).
    /// - Manual Next/Prev (arrows): jumps index forward/back (wrap optional) and plays that item.
    /// - Multiple mode: EVERY Play press starts a NEW queue session (new key).
    /// - SingleGlobal: Play resumes same queue (unless you clear it).
    ///
    /// Notes:
    /// - Queue content is Item list. Each Item stores SoundDefinition + PlayContext + resolved clipIndex.
    /// - EnqueueAllClips(s) calls QueueController.Enqueue with ctx.hasClipIndex==true.
    /// - We generate a unique queue key per "batch" (per Play press) for Multiple mode.
    /// </summary>
    public class QueueController
    {
        struct Item
        {
            public SoundDefinition sound;
            public PlayContext ctx;
            public int clipIndex;
        }

        /// <summary>
        /// Invoked when the active queue item starts playing (autoplay or Next/Prev).
        /// Args: owning sound, clip slot index on that sound (<see cref="Item.clipIndex"/>), not the playlist list position.
        /// </summary>
        public event System.Action<SoundDefinition, int> OnClipChanged;

        class ListQueue
        {
            public readonly List<Item> items = new List<Item>(16);

            /// <summary>Current position. -1 means "not started".</summary>
            public int index = -1;

            /// <summary>Wrap behavior for manual Next/Prev.</summary>
            public bool manualWrap = true;

            /// <summary>Wrap behavior for autoplay runner (usually false).</summary>
            public bool autoplayWrap = false;

            public int Count => items.Count;

            public void Clear()
            {
                items.Clear();
                index = -1;
            }

            public bool TryGetCurrent(out Item item)
            {
                item = default;
                if (items.Count == 0) return false;
                if (index < 0 || index >= items.Count) return false;
                item = items[index];
                return true;
            }

            public bool MoveNext(bool wrap)
            {
                if (items.Count == 0) return false;

                // If never started, start at 0
                if (index < 0)
                {
                    index = 0;
                    return true;
                }

                int next = index + 1;
                if (next >= items.Count)
                {
                    if (!wrap) return false;
                    next = 0;
                }

                index = next;
                return true;
            }

            public bool MovePrev(bool wrap)
            {
                if (items.Count == 0) return false;

                // If never started, "prev" means go to end (if wrap)
                if (index < 0)
                {
                    if (!wrap) return false;
                    index = items.Count - 1;
                    return true;
                }

                int prev = index - 1;
                if (prev < 0)
                {
                    if (!wrap) return false;
                    prev = items.Count - 1;
                }

                index = prev;
                return true;
            }
        }

        readonly Dictionary<int, ListQueue> queues = new();
        readonly Dictionary<int, Coroutine> runners = new();
        readonly HashSet<int> activeRunners = new();

        // For unique queue sessions in Multiple mode
        int _uniqueCounter = 0;

        // Same-frame batching for EnqueueAllClips (clipIndex==0 creates queue, rest reuses)
        readonly Dictionary<int, int> lastEnqueueFrameByBaseKey = new();
        readonly Dictionary<int, int> pendingFinalKeyByBaseKey = new();

        // The "active" key per SoundDefinition (latest created session)
        readonly Dictionary<SoundDefinition, int> activeKeyBySound = new();

        // ---------- Keying ----------
        static int MakeKey(QueueScope scope, int categoryHash, int soundId)
        {
            unchecked
            {
                int k = 17;
                k = k * 31 + (int)scope;
                k = k * 31 + categoryHash;
                k = k * 31 + soundId;
                return k;
            }
        }

        static int StableHash(string s)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(s)) return 0;
                const int fnvPrime = 16777619;
                int hash = (int)2166136261;
                for (int i = 0; i < s.Length; i++)
                    hash = (hash ^ s[i]) * fnvPrime;
                return hash;
            }
        }

        static int CategoryHash(SoundDefinition s)
        {
            if (s == null) return 0;

            if (s.useCustomCategory)
                return StableHash((s.customCategoryKey ?? "").Trim().ToLowerInvariant());

            return (int)s.category;
        }

        int BaseKeyFor(SoundDefinition s)
        {
            if (s == null) return 0;

            int catHash = CategoryHash(s);

            return s.queueScope switch
            {
                QueueScope.PerCategory => MakeKey(QueueScope.PerCategory, catHash, 0),
                _ => MakeKey(QueueScope.PerSound, catHash, s.GetInstanceID())
            };
        }

        int MakeUniqueKey(int baseKey)
        {
            unchecked
            {
                // XOR with a changing multiplier => different int each time
                _uniqueCounter++;
                return baseKey ^ (_uniqueCounter * 7919);
            }
        }

        ListQueue GetOrCreate(int key)
        {
            if (!queues.TryGetValue(key, out var q))
            {
                q = new ListQueue();
                queues[key] = q;
            }
            return q;
        }

        void StartRunnerIfNeeded(AudioSystem sys, int key)
        {
            if (sys == null) return;
            if (activeRunners.Contains(key)) return;

            activeRunners.Add(key);
            runners[key] = sys.StartCoroutine(Run(sys, key));
        }

        void StopRunner(AudioSystem sys, int key)
        {
            if (sys != null && runners.TryGetValue(key, out var co) && co != null)
                sys.StopCoroutine(co);

            runners.Remove(key);
            activeRunners.Remove(key);
        }

        void CleanupKey(int key)
        {
            // remove queue itself
            queues.Remove(key);

            // remove runner references (safety)
            runners.Remove(key);
            activeRunners.Remove(key);

            // clear activeKey mapping if it points to this key
            var toRemove = new List<SoundDefinition>();
            foreach (var kv in activeKeyBySound)
                if (kv.Value == key)
                    toRemove.Add(kv.Key);

            foreach (var s in toRemove)
                activeKeyBySound.Remove(s);
        }

        // ---------- Public API ----------

        /// <summary>
        /// Called by AudioSystem.Enqueue / EnqueueAllClips.
        /// - If ctx.hasClipIndex: add only that clipIndex.
        /// - Else: add ALL clips (0..n-1) into the queue.
        ///
        /// Multiple:
        /// - Normal Enqueue => always new queue session
        /// - EnqueueAllClips => clipIndex==0 creates new queue session, the rest in same frame reuse it
        ///
        /// SingleGlobal:
        /// - Uses baseKey always; does not create multiple sessions
        /// </summary>
        public void Enqueue(AudioSystem sys, SoundDefinition s, in PlayContext ctx)
        {
            if (sys == null || s == null) return;
            if (s.clips == null || s.clips.Count == 0) return;

            int baseKey = BaseKeyFor(s);
            int currentFrame = Time.frameCount;

            int finalKey;

            bool isSingleGlobal = s.instanceMode == InstanceMode.SingleGlobal;

            if (isSingleGlobal)
            {
                finalKey = baseKey;
            }
            else
            {
                // MULTIPLE
                if (ctx.hasClipIndex)
                {
                    // EnqueueAllClips batching (same frame)
                    // If a batch already started in the same frame:
                    // - even if clipIndex is 0, do NOT open a NEW queue, use the existing batch key.
                    if (pendingFinalKeyByBaseKey.TryGetValue(baseKey, out int cachedKey) &&
                        lastEnqueueFrameByBaseKey.TryGetValue(baseKey, out int lastFrame) &&
                        lastFrame == currentFrame)
                    {
                        finalKey = cachedKey; // same frame = same queue
                    }
                    else
                    {
                        // First time seeing this in this frame => new queue session
                        finalKey = MakeUniqueKey(baseKey);
                        pendingFinalKeyByBaseKey[baseKey] = finalKey;
                        lastEnqueueFrameByBaseKey[baseKey] = currentFrame;
                    }
                }
                else
                {
                    // Normal Enqueue => always new session
                    finalKey = MakeUniqueKey(baseKey);
                }
            }

            activeKeyBySound[s] = finalKey;

            var q = GetOrCreate(finalKey);

            // IMPORTANT: keep -1 until started (autoplay will start at 0)
            // (do not set q.index here)

            int ci = ctx.hasClipIndex ? ctx.clipIndex : 0; 
            if (ci >= 0 && ci < s.clips.Count)
                q.items.Add(new Item { sound = s, ctx = ctx, clipIndex = ci });
        }

        /// <summary>
        /// Stops runners and removes stale sessions for <paramref name="s"/> so overlapping QueueAll batches cannot emit duplicate callbacks (Multiple instance mode).
        /// </summary>
        public void AbortOtherSessionsForSound(AudioSystem sys, SoundDefinition s, int keepKey)
        {
            if (sys == null || s == null || keepKey == 0)
                return;

            var keys = new List<int>(queues.Keys);
            foreach (int key in keys)
            {
                if (key == keepKey)
                    continue;

                if (!queues.TryGetValue(key, out ListQueue q) || q == null || q.Count == 0)
                    continue;

                if (q.items[0].sound != s)
                    continue;

                StopRunner(sys, key);
                CleanupKey(key);
            }
        }

        /// <summary>
        /// Play button behavior.
        ///
        /// Multiple mode: ALWAYS starts a NEW session by calling sys.EnqueueAllClips().
        /// SingleGlobal: resumes existing session (does not create new queue).
        /// </summary>
        public void PlayOrResume(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null) return;
            if (s.clips == null || s.clips.Count == 0) return;

        if (s.instanceMode == InstanceMode.SingleGlobal)
        {
            sys.EnqueueAllClips(s, PlayContext.Default);
            
            int key = GetActiveKeyFor(s);
            if (key == 0) return;

            if (!queues.TryGetValue(key, out var q) || q.Count == 0) return;
            StartRunnerIfNeeded(sys, key);
            return;
        }

            sys.EnqueueAllClips(s, PlayContext.Default);

            int newKey = GetActiveKeyFor(s);
            if (newKey == 0) return;

            AbortOtherSessionsForSound(sys, s, newKey);

            if (queues.TryGetValue(newKey, out var newQ))
                newQ.index = -1;

            StartRunnerIfNeeded(sys, newKey);
        }


        /// <summary>
        /// Manual Next: stops runner, moves index +1 (wrap), plays that item.
        /// Does NOT automatically restart runner (user can press Play to autoplay).
        /// </summary>
        public bool PlayNext(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null) return false;

            int key = GetActiveKeyFor(s);
            if (key == 0 || !queues.TryGetValue(key, out var q) || q.Count == 0)
                return false;

            StopRunner(sys, key);
            sys.StopAllInstances(s);

            if (!q.MoveNext(q.manualWrap))
                return false;

            if (!q.TryGetCurrent(out var item))
                return false;

            PlayItemBypassQueue(sys, item);
            OnClipChanged?.Invoke(item.sound, item.clipIndex);
            return true;
        }

        /// <summary>
        /// Manual Prev: stops runner, moves index -1 (wrap), plays that item.
        /// </summary>
        public bool PlayPrev(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null) return false;

            int key = GetActiveKeyFor(s);
            if (key == 0 || !queues.TryGetValue(key, out var q) || q.Count == 0)
                return false;

            StopRunner(sys, key);
            sys.StopAllInstances(s);

            if (!q.MovePrev(q.manualWrap))
                return false;

            if (!q.TryGetCurrent(out var item))
                return false;

            PlayItemBypassQueue(sys, item);
            OnClipChanged?.Invoke(item.sound, item.clipIndex);
            return true;
        }

        /// <summary>
        /// Skip one item (removes current if valid, otherwise removes first).
        /// </summary>
        public bool DropOne(SoundDefinition s)
        {
            if (s == null) return false;

            int key = GetActiveKeyFor(s);
            if (key == 0 || !queues.TryGetValue(key, out var q) || q.Count == 0)
                return false;

            int removeIndex = (q.index >= 0 && q.index < q.Count) ? q.index : 0;
            q.items.RemoveAt(removeIndex);

            if (q.Count == 0)
            {
                q.index = -1;
                CleanupKey(key);
            }
            else if (q.index >= q.Count)
            {
                q.index = q.Count - 1;
            }

            return true;
        }

        public int GetCount(SoundDefinition s)
        {
            int key = GetActiveKeyFor(s);
            if (key == 0) return 0;
            return queues.TryGetValue(key, out var q) ? q.Count : 0;
        }

        public bool IsRunning(SoundDefinition s)
        {
            int key = GetActiveKeyFor(s);
            return key != 0 && activeRunners.Contains(key);
        }

        public (int currentIndex, int totalCount) GetQueuePosition(SoundDefinition s)
        {
            int key = GetActiveKeyFor(s);
            if (key == 0 || !queues.TryGetValue(key, out var q)) return (-1, 0);
            return (q.index, q.Count);
        }

        public void Clear(AudioSystem sys = null)
        {
            if (sys != null)
            {
                foreach (var kv in runners)
                    if (kv.Value != null) sys.StopCoroutine(kv.Value);
            }

            queues.Clear();
            runners.Clear();
            activeRunners.Clear();
            activeKeyBySound.Clear();
            pendingFinalKeyByBaseKey.Clear();
            lastEnqueueFrameByBaseKey.Clear();
        }

        public void Clear(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null) return;

            // remove all queues that belong to this sound
            var toRemove = new List<int>();
            foreach (var kv in queues)
            {
                var q = kv.Value;
                if (q == null || q.Count == 0) continue;
                // any item sound match
                if (q.items[0].sound == s) toRemove.Add(kv.Key);
            }

            foreach (var k in toRemove)
            {
                StopRunner(sys, k);
                queues.Remove(k);
            }

            activeKeyBySound.Remove(s);
        }

        public List<QueuedSoundSnapshot> GetQueuedSoundsSnapshot()
        {
            var result = new List<QueuedSoundSnapshot>();

            foreach (var kv in queues)
            {
                var q = kv.Value;
                if (q == null || q.Count == 0) continue;

                int idx = Mathf.Clamp(q.index, 0, q.Count - 1);
                var sd = q.items[idx].sound;

                result.Add(new QueuedSoundSnapshot
                {
                    SoundName = sd != null ? $"{sd.name} (Key:{kv.Key})" : "(null)",
                    QueuedCount = q.Count,
                    IsRunning = activeRunners.Contains(kv.Key),
                    Sound = sd
                });
            }

            return result;
        }

        // ---------- Internals ----------

        int GetActiveKeyFor(SoundDefinition s)
        {
            if (s == null) return 0;

            // Prefer explicitly tracked active key
            if (activeKeyBySound.TryGetValue(s, out int k) && k != 0 && queues.ContainsKey(k))
                return k;

            // Fallback: scan
            int latest = 0;
            foreach (var key in queues.Keys)
            {
                var q = queues[key];
                if (q == null || q.Count == 0) continue;
                if (q.items[0].sound == s)
                    latest = key;
            }

            if (latest != 0)
                activeKeyBySound[s] = latest;

            return latest;
        }

        void PlayItemBypassQueue(AudioSystem sys, in Item item)
        {
            if (sys == null || item.sound == null) return;

            // Build a ctx that has explicit clip index so TryResolveClipEntry returns that entry.
            var ctx = item.ctx;
            ctx = ctx.SetClipIndex(item.clipIndex);

            sys.PlayImmediateFromQueue(item.sound, ctx);
        }

        IEnumerator Run(AudioSystem sys, int key)
        {
            if (!queues.TryGetValue(key, out var q))
                yield break;

            // Autoplay should start from the first item
            if (q.index < 0) q.index = 0;

            while (!AudioSystem.IsShuttingDown && sys != null)
            {
                if (!queues.TryGetValue(key, out q) || q.Count == 0)
                    break;

                // index out of range guard
                if (q.index < 0) q.index = 0;
                if (q.index >= q.Count)
                {
                    if (q.autoplayWrap) q.index = 0;
                    else break;
                }

                var item = q.items[q.index];
                if (item.sound == null)
                {
                    // bozuk item -> next
                    if (!q.MoveNext(q.autoplayWrap)) break;
                    continue;
                }

                // resolve clip
                if (!sys.TryResolveClipEntry(item.sound, item.ctx, out var entry) || entry.clip == null)
                {
                    if (!q.MoveNext(q.autoplayWrap)) break;
                    continue;
                }

                // delay
                float totalDelay = sys.ResolveSoundDelay(item.sound) + Mathf.Max(0f, entry.delay);
                if (totalDelay > 0f)
                    yield return new WaitForSecondsRealtime(totalDelay);

                // play
                var voice = sys.PlayImmediateResolved(item.sound, item.ctx, entry.clip);

                OnClipChanged?.Invoke(item.sound, item.clipIndex);

                if (voice == null)
                {
                    // play failed -> wait one frame, then next
                    yield return null;
                    if (!q.MoveNext(q.autoplayWrap)) break;
                    continue;
                }

                // FIX: AudioSource may have isPlaying=false on the first frame.
                // Start grace: wait up to 0.15s for it to start.
                float startGrace = 0.15f;
                float sg = 0f;
                while (!AudioSystem.IsShuttingDown && sg < startGrace)
                {
                    sg += Time.unscaledDeltaTime;
                    if (voice != null && voice.IsPlaying())
                        break;
                    yield return null;
                }

                // If looping: the loop won't end, so don't continue autoplay (your preference)
                // If you want autoplay to continue even on loop, remove this block.
                if (item.sound.loop)
                {
                    // loop stays on "single item", don't move to next
                    // You can stop the runner here if you want:
                    break;
                }

                // Main wait: protect with timeout even if isPlaying returns false
                float timeout = entry.clip.length + 0.25f;
                float t = 0f;

                while (!AudioSystem.IsShuttingDown && t < timeout)
                {
                    t += Time.unscaledDeltaTime;

                    // If started, wait until finished
                    if (voice == null) break;

                    // Exit if isPlaying becomes false after starting
                    if (t > 0.05f && !voice.IsPlaying())
                        break;

                    yield return null;
                }

                // move to next item
                if (!q.MoveNext(q.autoplayWrap))
                    break;
            }

            // At the end of Run coroutine, before CleanupKey:
            CleanupKey(key);
        }

        /// <summary>
        /// After <see cref="Enqueue"/> appends item(s), starts the autoplay runner for that queue if it is not already running.
        /// Use with <see cref="SoundDefinition.instanceMode"/> <see cref="InstanceMode.SingleGlobal"/> and shared
        /// <see cref="QueueScope.PerCategory"/> so different sounds append to one sequential list.
        /// </summary>
        public void NotifyEnqueueCompleted(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null)
                return;

            int key = GetActiveKeyFor(s);
            if (key != 0)
                StartRunnerIfNeeded(sys, key);
        }
    }
}
