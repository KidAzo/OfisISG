using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoiUtils.AudioSystem
{
	// ======== Debug Snapshot Structs (Editor use) ========
	
	/// <summary>
	/// Lightweight snapshot of an active AudioVoice for editor debugging.
	/// </summary>
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
		public string DebugStatus; // New field to debug why it might be filtered
	}

	/// <summary>
	/// Lightweight snapshot of queued sounds for editor debugging.
	/// </summary>
	public struct QueuedSoundSnapshot
	{
		public string SoundName;
		public int QueuedCount;
		public bool IsRunning;
		public SoundDefinition Sound; // Reference for control buttons
	}

	public class AudioSystem : MonoBehaviour
	{
		[SerializeField] AudioSystemConfig config;	
		public static bool IsShuttingDown { get; private set; } = false;
		AudioPoolAdapter adapter;

		private readonly Dictionary<SoundDefinition, float> lastPlayTime = new();   // cooldown
		private readonly Dictionary<SoundDefinition, AudioVoice> singleGlobals = new(); // instance
		private readonly Dictionary<SoundDefinition, int> lastRandomIndex = new();
		private readonly Dictionary<SoundDefinition, int> sequenceIndex = new();

		private readonly QueueController queue = new();

		private readonly LinkedList<AudioVoice> activeOrder = new(); // oldest -> newest
		private readonly Dictionary<AudioVoice, LinkedListNode<AudioVoice>> activeNodes = new();
		// AudioSystem.cs

		readonly Dictionary<SoundDefinition, float> cooldowns = new();
		public int ActiveVoiceCount => activeOrder.Count;

		// ---------------- Queue API ----------------
		public int GetQueueCount(SoundDefinition s) => queue.GetCount(s);

		public void ClearQueue() => queue.Clear(this);

		public void ClearQueue(SoundDefinition s) => queue.Clear(this, s);

		public bool SkipQueueOne(SoundDefinition s) => queue.DropOne(s);

		// ---------------- Debug Snapshot API (Editor) ----------------

		/// <summary>
		/// Returns a snapshot of all currently active voices.
		/// Includes inactive GameObjects that are still tracked (potential leaks) for debugging.
		/// </summary>
		public List<ActiveVoiceSnapshot> GetActiveVoicesSnapshot()
		{
			var result = new List<ActiveVoiceSnapshot>(activeOrder.Count);

			// Iterate using node traversal to be safe
			var node = activeOrder.First;
			while (node != null)
			{
				var voice = node.Value;
				node = node.Next;
				
				// Capture extremely raw state for debugging
				bool isRefNull = ReferenceEquals(voice, null);
				bool isUnityNull = !isRefNull && voice == null; // Overloaded check
				bool isActive = !isRefNull && !isUnityNull && voice.gameObject.activeInHierarchy;

				string status = "OK";
				if (isRefNull) status = "NULL_REF";
				else if (isUnityNull) status = "DESTROYED";
				else if (!isActive) status = "INACTIVE_GO";

				// If it's a solid null, we can't access data, but we should report it exists
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

				// Safe to access properties now
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

		/// <summary>
		/// Returns a snapshot of all queued sounds.
		/// </summary>
		public List<QueuedSoundSnapshot> GetQueuedSoundsSnapshot()
		{
			if (queue == null) return new List<QueuedSoundSnapshot>();
			return queue.GetQueuedSoundsSnapshot();
		}
		
		void OnApplicationQuit() => IsShuttingDown = true;

		void Awake()
		{
			IsShuttingDown = false;

			adapter = GetComponent<AudioPoolAdapter>();
			if (adapter == null)
				Debug.LogError("AudioSystem requires AudioPoolAdapter on the same GameObject.");
		}

		bool RegisterVoice(AudioVoice voice)
		{
			if (voice == null) return false;

			// zaten kayıtlıysa (bug/edge-case) yeniden ekleme
			if (activeNodes.ContainsKey(voice))
				return false;

			// limit aşıldıysa en eskiyi steal et
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

			// Aktif sesler varsa stoplamaya çalışma (Unity teardown sırasında riskli olabilir)
			// ama çoğu durumda safe:
			if (activeNodes != null && activeNodes.Count > 0)
			{
				var temp = new List<AudioVoice>(activeOrder);
				foreach (var v in temp)
					if (v != null) v.Stop();
			}
		}

		void OnTakeFromPool(AudioVoice v)
		{
			// Sen AudioVoice içinde Get() istiyorum demiştin
			v.Get();
		}

		void OnReturnedToPool(AudioVoice v)
		{
			// Node vb temizliği AudioVoice.Release içinde zaten yapılabilir
			v.Release();
		}

		void OnDestroyPoolObject(AudioVoice v)
		{
			if (v != null) Destroy(v.gameObject);
		}

		// ---------------- Public API ----------------

		public AudioVoice Play(SoundDefinition sound) => Play(sound, PlayContext.Default);

		public AudioVoice PlayAt(SoundDefinition sound, Vector3 position) => Play(sound, PlayContext.At(position));

		public AudioVoice PlayFollow(SoundDefinition sound, Transform follow) => Play(sound, PlayContext.Follow(follow));

		public void Enqueue(SoundDefinition sound) => Enqueue(sound, PlayContext.Default);

		public void Enqueue(SoundDefinition sound, in PlayContext ctx)
		{
			if (sound == null) return;
			queue.Enqueue(this, sound, ctx);
		}

		/// <summary>
		/// Enqueues ALL clips from the SoundDefinition to play sequentially.
		/// </summary>
		public void EnqueueAllClips(SoundDefinition sound) => EnqueueAllClips(sound, PlayContext.Default);

		public void EnqueueAllClips(SoundDefinition sound, in PlayContext ctx)
		{
			if (sound == null || sound.clips == null) return;

			// Block new requests if queue is already running AND suppressDuplicatesWhileQueued is enabled
			if (sound.suppressDuplicatesWhileQueued && queue.IsRunning(sound))
			{
				Debug.Log($"[AudioSystem] EnqueueAllClips blocked - queue already running for {sound.name}");
				return;
			}

			Debug.Log($"[AudioSystem] EnqueueAllClips called for {sound.name}, clips count: {sound.clips.Count}");

			for (int i = 0; i < sound.clips.Count; i++)
			{
				var c = ctx;
				c = c.SetClipIndex(i);
				Debug.Log($"[AudioSystem] Enqueuing clip index {i} for {sound.name}");
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
			{
				voice.Stop(); // Stop() => ReturnVoice çağıracak
			}

			singleGlobals.Remove(sound);
		}

		// --------------- Core play router ---------------

	public AudioVoice Play(SoundDefinition sound, in PlayContext ctx)
{
    if (sound == null) return null;

    // QueueAll mode: queue all clips and return
    if (sound.selectionMode == ClipSelectionMode.QueueAll && !ctx.hasClipIndex)
    {
        EnqueueAllClips(sound, ctx);
        return null;
    }

    // Queue policy: queue ALL clips when ScheduleMode is Queue
    if (!ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue && !ctx.hasClipIndex)
    {
        EnqueueAllClips(sound, ctx);
        return null;
    }

    // Single clip queue (when clipIndex is already set from EnqueueAllClips)
    if (!ctx.ignoreCooldowns && sound.scheduleMode == ScheduleMode.Queue && ctx.hasClipIndex)
    {
        queue.Enqueue(this, sound, ctx);
        return null;
    }

    // Resolve clip entry ONCE (so clip-specific delay matches the chosen clip)
    if (!TryResolveClipEntry(sound, ctx, out var clipEntry))
        return null;

    if (clipEntry.clip == null)
        return null;

    // Total delay = sound delay + clip delay (unless debug bypass)
    float totalDelay = 0f;

    if (!ctx.ignoreCooldowns)
    {
        totalDelay += ResolveSoundDelay(sound);
        totalDelay += Mathf.Max(0f, clipEntry.delay);
    }

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
        DelayMode.Fixed       => Mathf.Max(0f, sound.delay),
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



		IEnumerator DelayedPlay(SoundDefinition sound, PlayContext ctx)
		{
			float d = 0f;
			if (sound.delayMode == DelayMode.Fixed)
				d = Mathf.Max(0f, sound.delay);
			else if (sound.delayMode == DelayMode.RandomRange)
				d = Mathf.Max(0f, Random.Range(sound.delayRange.x, sound.delayRange.y));

			if (d > 0f) yield return new WaitForSecondsRealtime(d);

			PlayImmediate(sound, ctx);
		}

		// QueueController burayı çağırıyor
		internal AudioVoice PlayImmediate(SoundDefinition sound, in PlayContext ctx)
		{
			if (adapter == null) return null;

			if (sound == null) return null;

			// -------- Cooldown (anti-spam) --------
			if (!ctx.ignoreCooldowns && sound.cooldown > 0f)
			{
				float now = Time.unscaledTime;
				if (lastPlayTime.TryGetValue(sound, out float last))
				{
					if ((now - last) < sound.cooldown)
						return null;
				}
			}

			// -------- SingleGlobal instance --------
			if (sound.instanceMode == InstanceMode.SingleGlobal)
			{
				if (singleGlobals.TryGetValue(sound, out var existing) && existing != null)
				{
   					 	if (!ctx.ignoreCooldowns && sound.reTriggerMode == ReTriggerMode.Ignore)
						return existing;

					// Restart
					existing.Stop();               // Stop() -> ReturnVoice
					singleGlobals.Remove(sound);   // registry temizle (null set yerine remove daha temiz)
				}
			}

			// -------- Clip selection (multi-clip) --------
			if (!TryResolveClipEntry(sound, ctx, out var clipEntry))
				return null;

			var clip = clipEntry.clip;
			
			float clipDelay = Mathf.Max(0f, clipEntry.delay);
			
			if (clip == null) return null;

			// -------- Pool'dan voice al --------
			var voice = adapter.Get();

			// Bind + Apply
			voice.Bind(sound, this);
			voice.Apply(sound, clip, ctx);

			// "MarkPlayed" (cooldown timestamp)
			lastPlayTime[sound] = Time.unscaledTime;

			if (IsShuttingDown) { adapter.Return(voice); return null; }

			if (!RegisterVoice(voice))
			{
				// couldn't register due to protected voices. drop this play.
				adapter.Return(voice);
				return null;
			}

			voice.Play();

			// Registry set
			if (sound.instanceMode == InstanceMode.SingleGlobal)
				singleGlobals[sound] = voice;

			return voice;
		}
internal AudioVoice PlayImmediateResolved(SoundDefinition sound, in PlayContext ctx, AudioClip clip)
{
    if (adapter == null || sound == null || clip == null) return null;

    // cooldown (ignoreCooldowns varsa bypass)
    if (!ctx.ignoreCooldowns && sound.cooldown > 0f)
    {
        float now = Time.unscaledTime;
        if (lastPlayTime.TryGetValue(sound, out float last) && (now - last) < sound.cooldown)
            return null;
    }

    // SingleGlobal
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


	public bool TryResolveClipEntry(
    SoundDefinition sound,
    in PlayContext ctx,
    out ClipEntry result)
{
    result = default;

    if (sound == null || sound.clips == null || sound.clips.Count == 0)
        return false;

    // 0) Explicit clip override
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

private bool TryResolveSequence(SoundDefinition sound, out ClipEntry result)
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

private bool TryResolveRandomWeighted(SoundDefinition sound, out ClipEntry result)
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




	bool IsInCooldown(SoundDefinition def)
	{
		if (def == null)
			return false;

		if (def.cooldown <= 0f)
			return false;

		if (!cooldowns.TryGetValue(def, out float nextTime))
			return false;

		return Time.time < nextTime;
	}

	void SetCooldown(SoundDefinition def)
	{
		if (def == null)
			return;

		if (def.cooldown <= 0f)
			return;

		cooldowns[def] = Time.time + def.cooldown;
	}

	private AudioClip ResolveSequence(SoundDefinition sound)
	{
		int count = sound.clips.Count;
		if (count == 0) return null;

		if (!sequenceIndex.TryGetValue(sound, out int idx))
			idx = 0;

		if (idx < 0 || idx >= count) idx = 0;

		var clip = sound.clips[idx].clip;

		idx = (idx + 1) % count;
		sequenceIndex[sound] = idx;

		return clip;
	}

	private AudioClip ResolveRandomWeighted(SoundDefinition sound)
	{
		int count = sound.clips.Count;
		if (count == 0) return null;

		int chosen = ChooseWeightedIndex(sound);

		// No immediate repeat
		if (sound.noImmediateRepeat && count > 1)
		{
			if (lastRandomIndex.TryGetValue(sound, out int last) && chosen == last)
			{
				// retry a few times
				for (int k = 0; k < 3; k++)
				{
					int retry = ChooseWeightedIndex(sound);
					if (retry != last) { chosen = retry; break; }
				}

				// still same -> force different
				if (chosen == last)
					chosen = (last + 1) % count;
			}
		}

		lastRandomIndex[sound] = chosen;
		return sound.clips[chosen].clip;
	}

	private int ChooseWeightedIndex(SoundDefinition sound)
	{
		int count = sound.clips.Count;

		float total = 0f;
		for (int i = 0; i < count; i++)
			total += Mathf.Max(0f, sound.clips[i].weight);

		// If all weights are 0 => uniform random
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



		// AudioVoice -> Stop/End -> buraya düşer
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
