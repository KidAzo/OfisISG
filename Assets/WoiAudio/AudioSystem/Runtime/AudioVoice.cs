using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WoiUtils.Pooling;

namespace WoiUtils.AudioSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioVoice : MonoBehaviour, IPoolable
    {
        public SoundDefinition Data { get; private set; }
        public LinkedListNode<AudioVoice> Node { get; set; }

        AudioSystem owner;
        AudioSource audioSource;

        bool isPaused;
        bool isReturning;

        float pausedTime;

        Coroutine endRoutine;
        Coroutine followRoutine;
        Coroutine fadeRoutine;

        // Pool reuse güvenliği için (handle ile kullanacağız)
        public int Generation { get; private set; }

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        void OnDisable()
        {
            StopFollow();
        }

        /// <summary> Pool'dan alındığında AudioSystem bunu çağırır. </summary>
        public void Bind(SoundDefinition data, AudioSystem owner)
        {
            Data = data;
            this.owner = owner;
          
            isReturning = false;
        }

        /// <summary> SoundDefinition + seçilmiş clip + context'i AudioSource'a uygular. </summary>
        public void Apply(SoundDefinition data, AudioClip clip, in PlayContext ctx)
        {
            // Core: clip multi destek
            audioSource.clip = clip;

            audioSource.outputAudioMixerGroup = data.mixerGroup;
            audioSource.loop = data.loop;
            audioSource.playOnAwake = false; // runtime voice için genelde false

            audioSource.mute = data.mute;
            audioSource.bypassEffects = data.bypassEffects;
            audioSource.bypassListenerEffects = data.bypassListenerEffects;
            audioSource.bypassReverbZones = data.bypassReverbZones;


            audioSource.priority = data.priority;
            
            audioSource.volume = Mathf.Clamp(
                data.volume * ctx.volumeMul,
                0f,
                1.5f
            );

            audioSource.pitch  = data.pitch * (ctx.pitchMul <= 0 ? 1f : ctx.pitchMul);
            audioSource.panStereo = data.panStereo;
            audioSource.spatialBlend = data.spatialBlend;

            Debug.Log($"[AudioVoice] Applying SoundDefinition settings to AudioSource for clip '{audioSource.spatialBlend}'");
            
            audioSource.reverbZoneMix = data.reverbZoneMix;
            audioSource.dopplerLevel = data.dopplerLevel;
            audioSource.spread = data.spread;

            audioSource.minDistance = data.minDistance;
            audioSource.maxDistance = data.maxDistance;
            audioSource.rolloffMode = data.rolloffMode;

            audioSource.ignoreListenerVolume = data.ignoreListenerVolume;
            audioSource.ignoreListenerPause = data.ignoreListenerPause;

            // Position / Follow (senin isteğin 3D ise field açılacak -> ctx ile)
            if (ctx.hasFollow && ctx.follow != null)
            {
                transform.position = ctx.follow.position;
                StartFollow(ctx.follow);
            }
            else
            {
                StopFollow();
                if (ctx.hasPosition) transform.position = ctx.position;
            }
        }

        public void Play()
        {
            // önceki rutinleri temizle
            if (endRoutine != null) StopCoroutine(endRoutine);
            endRoutine = null;

            isPaused = false;
            pausedTime = 0f;

            audioSource.Play();

            // loop ise otomatik bitiş bekleme yok; dışarıdan Stop gelir
            if (!audioSource.loop)
                endRoutine = StartCoroutine(WaitForEndCoroutine());
        }

        IEnumerator WaitForEndCoroutine()
        {
            // Pause durumunda isPlaying false olur, o yüzden paused guard lazım.
            while (!isReturning && audioSource != null && (audioSource.isPlaying || isPaused))
                yield return null;

            ReturnToPool();
        }

      public void Stop()
        {
            // Even if already returning, at least ensure audio is silenced
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();

            if (isReturning) return;

            if (endRoutine != null) StopCoroutine(endRoutine);
            endRoutine = null;

            StopFollow();

            isPaused = false;
            pausedTime = 0f;

            ReturnToPool();
        }

        void ReturnToPool()
        {
            if (isReturning) return;
            isReturning = true;

            if (AudioSystem.IsShuttingDown)
                return;

            // owner sahne unload sırasında "fake null" olabilir
            if (owner == null)
                return;

            owner.ReturnVoice(this);
        }

        public void Pause()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                isPaused = true;
                pausedTime = audioSource.time;
                audioSource.Pause();
            }
        }

        public void UnPause()
        {
            if (audioSource != null && isPaused)
            {
                isPaused = false;
                if (audioSource.clip != null)
                    audioSource.time = Mathf.Min(pausedTime, audioSource.clip.length - 0.01f);

                audioSource.UnPause();
            }
        }

        /// <summary> DOTween yok. Coroutine ile fade. </summary>
        public void SetVolume(float targetVolume, float duration, Action onComplete = null)
        {
              if (fadeRoutine != null) StopCoroutine(fadeRoutine);
                fadeRoutine = null;   

            // duration 0 ise direkt set
            if (duration <= 0f)
            {
                if (audioSource != null) audioSource.volume = targetVolume;
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(FadeVolumeCoroutine(targetVolume, duration, onComplete));
        }

        IEnumerator FadeVolumeCoroutine(float target, float duration, Action onComplete)
        {
            fadeRoutine = null;

            float start = audioSource != null ? audioSource.volume : 0f;
            float t = 0f;

            while (t < duration && audioSource != null)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / duration);
                audioSource.volume = Mathf.Lerp(start, target, a);
                yield return null;
            }

            if (audioSource != null) audioSource.volume = target;
            onComplete?.Invoke();
        }

        public void WithRandomPitch(float min = -0.05f, float max = 0.05f)
        {
            if (audioSource == null) return;
            audioSource.pitch += UnityEngine.Random.Range(min, max);
        }

        // --- Pool hooks (senin istediğin gibi AudioVoice içinde) ---
        public void Get()
        {
            Generation++;

            // Reactivate first
            gameObject.SetActive(true);

            // HARD reset runtime state
            isReturning = false;
            isPaused = false;
            pausedTime = 0f;

            // kill coroutines
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;

            if (endRoutine != null) StopCoroutine(endRoutine);
            endRoutine = null;

            StopFollow();

            // reset AudioSource
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.time = 0f;
                // clip'i burada nulllama; Apply zaten clip set ediyor.
                // ama safety istersen:
                audioSource.clip = null;

                // volume/pitch fade’den kalmış olabilir -> defaulta çek
                audioSource.volume = 1f;
                audioSource.pitch = 1f;
                audioSource.mute = false;
            }
        }


        public void Release()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            
            // güvenlik: coroutine ve state temizle
            if (endRoutine != null) StopCoroutine(endRoutine);
                endRoutine = null;

            StopFollow();

            isPaused = false;
            pausedTime = 0f;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            Data = null;
            owner = null;
            Node = null;

            isReturning = false;

            gameObject.SetActive(false);
        }

        public bool IsPlaying()
        {
            return audioSource != null && audioSource.isPlaying;
        }

        // --- Debug Helpers ---

        public string GetCurrentClipName()
        {
            if (audioSource == null || audioSource.clip == null) return "(none)";
            return audioSource.clip.name;
        }

        public bool HasFollowTarget()
        {
            return followRoutine != null;
        }

        void StartFollow(Transform t)
        {
            if (followRoutine != null) StopCoroutine(followRoutine);
            followRoutine = StartCoroutine(FollowRoutine(t));
        }

        void StopFollow()
        {
            if (followRoutine != null) StopCoroutine(followRoutine);
            followRoutine = null;
        }

        IEnumerator FollowRoutine(Transform t)
        {
            while (!isReturning && !AudioSystem.IsShuttingDown && t != null && gameObject.activeInHierarchy)
            {
                transform.position = t.position;
                yield return null;
            }
        }
    }
}
