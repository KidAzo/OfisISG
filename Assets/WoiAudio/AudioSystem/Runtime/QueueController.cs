using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoiUtils.AudioSystem
{
    public class QueueController
    {
        readonly Dictionary<int, Queue<Item>> queues = new();
        readonly Dictionary<int, Coroutine> runners = new();
        readonly HashSet<int> activeRunners = new(); // Track which runners are actively running

        struct Item
        {
            public SoundDefinition sound;
            public PlayContext ctx;
        }

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

        int KeyFor(SoundDefinition s)
        {
            if (s == null) return 0;

            int catHash = CategoryHash(s);

            return s.queueScope switch
            {
                QueueScope.PerCategory => MakeKey(s.queueScope, catHash, 0),
                _ => MakeKey(s.queueScope, catHash, s.GetInstanceID())
            };
        }

        public void Enqueue(AudioSystem sys, SoundDefinition s, in PlayContext ctx)
        {
            if (sys == null || s == null) return;

            int key = KeyFor(s);

            if (!queues.TryGetValue(key, out var q))
                queues[key] = q = new Queue<Item>();

            var c = ctx;
            c.queued = true;
            // Queue items should bypass cooldowns - the queue itself handles sequencing
            c.ignoreCooldowns = true;

            q.Enqueue(new Item { sound = s, ctx = c });
            
            Debug.Log($"[QueueController] Enqueued {s.name} to key {key}, queue count now: {q.Count}");

            // Start runner if not already running
            if (!activeRunners.Contains(key))
            {

                activeRunners.Add(key);
                runners[key] = sys.StartCoroutine(Run(sys, key));
            }
        }

        IEnumerator Run(AudioSystem sys, int key)
        {
            Debug.Log($"[QueueController] Run started for key {key}");
            
            while (!AudioSystem.IsShuttingDown && sys != null)
            {
                if (!queues.TryGetValue(key, out var q) || q.Count == 0)
                {
                    Debug.Log($"[QueueController] Queue empty or not found for key {key}, exiting runner");
                    break;
                }

                Debug.Log($"[QueueController] Queue count: {q.Count} for key {key}");

                var item = q.Peek();
                if (item.sound == null)
                {
                    Debug.Log($"[QueueController] Item sound is null, dequeuing");
                    q.Dequeue();
                    continue;
                }

                // 1) ClipEntry resolve
                if (!sys.TryResolveClipEntry(item.sound, item.ctx, out var entry) || entry.clip == null)
                {
                    Debug.Log($"[QueueController] Failed to resolve clip for {item.sound.name}, dequeuing");
                    q.Dequeue();
                    yield return null;
                    continue;
                }

                Debug.Log($"[QueueController] Playing {item.sound.name} with clip {entry.clip.name}");

                // 2) total delay (sound delay + clip delay)
                float totalDelay =
                    sys.ResolveSoundDelay(item.sound) +
                    Mathf.Max(0f, entry.delay);

                if (totalDelay > 0f)
                    yield return new WaitForSecondsRealtime(totalDelay);

                // 3) Now try to actually play
                var voice = sys.PlayImmediateResolved(item.sound, item.ctx, entry.clip);

                if (voice == null)
                {
                    Debug.Log($"[QueueController] PlayImmediateResolved returned null for {item.sound.name}, retrying next frame");
                    yield return null;
                    continue;
                }

                Debug.Log($"[QueueController] Successfully started playing {item.sound.name}, dequeuing");
                q.Dequeue();

                // Loop sounds should not block the queue
                if (item.sound.loop)
                {
                    yield return null;
                    continue;
                }

                // Wait for voice to finish
                Debug.Log($"[QueueController] Waiting for voice to finish...");
                while (!AudioSystem.IsShuttingDown && voice != null && voice.IsPlaying())
                    yield return null;
                
                Debug.Log($"[QueueController] Voice finished, continuing to next item");
            }

            Debug.Log($"[QueueController] Runner exiting for key {key}");
            activeRunners.Remove(key);
            runners.Remove(key);

            if (queues.TryGetValue(key, out var q2) && q2.Count == 0)
                queues.Remove(key);
        }

        // ---- Public controls ----

        public void Clear(AudioSystem sys = null)
        {
            queues.Clear();

            var cprunners = new Dictionary<int, Coroutine>(runners);

            if (sys != null)
            {
                foreach (var kv in cprunners)
                    if (kv.Value != null) sys.StopCoroutine(kv.Value);
            }

            runners.Clear();
            activeRunners.Clear();
        }

        public void Clear(AudioSystem sys, SoundDefinition s)
        {
            if (sys == null || s == null) return;

            int key = KeyFor(s);

            queues.Remove(key);

            if (runners.TryGetValue(key, out var co) && co != null)
                sys.StopCoroutine(co);

            runners.Remove(key);
            activeRunners.Remove(key);
        }

        public bool DropOne(SoundDefinition s)
        {
            if (s == null) return false;

            int key = KeyFor(s);

            if (!queues.TryGetValue(key, out var q) || q.Count == 0)
                return false;

            q.Dequeue();
            return true;
        }

        public int GetCount(SoundDefinition s)
        {
            if (s == null) return 0;
            int key = KeyFor(s);
            return queues.TryGetValue(key, out var q) ? q.Count : 0;
        }

        /// <summary>
        /// Returns true if a queue runner is actively processing this sound.
        /// </summary>
        public bool IsRunning(SoundDefinition s)
        {
            if (s == null) return false;
            int key = KeyFor(s);
            return activeRunners.Contains(key);
        }

        public List<QueuedSoundSnapshot> GetQueuedSoundsSnapshot()
        {
            var result = new List<QueuedSoundSnapshot>();
            var seenSounds = new Dictionary<SoundDefinition, (int count, bool running)>();

            foreach (var kvp in queues)
            {
                if (kvp.Value == null || kvp.Value.Count == 0) continue;

                // Peek at first item to identify the sound definition
                var items = kvp.Value.ToArray();
                foreach (var item in items)
                {
                    if (item.sound == null) continue;

                    if (!seenSounds.ContainsKey(item.sound))
                    {
                        bool isRunning = activeRunners.Contains(kvp.Key);
                        seenSounds[item.sound] = (0, isRunning);
                    }

                    var current = seenSounds[item.sound];
                    seenSounds[item.sound] = (current.count + 1, current.running);
                }
            }

            foreach (var kvp in seenSounds)
            {
                result.Add(new QueuedSoundSnapshot
                {
                    SoundName = kvp.Key.name,
                    QueuedCount = kvp.Value.count,
                    IsRunning = kvp.Value.running,
                    Sound = kvp.Key
                });
            }
            return result;
        }
    }
}
