using UnityEngine;
using WoiUtils.AudioSystem;
using Woi.PopUpSystem;
using System.Collections.Generic;
using Reflex.Attributes;
using Cysharp.Threading.Tasks;
using Woi.Events;
using System.Threading;
using System;

namespace Woi.FeedbackManager
{
    public class FeedbackManager : MonoBehaviour
    {
        [Inject] private AudioSystem audioSystem;
        [Inject] private PopupManager popupManager;
        [SerializeField] private PopupData popupData;
        private FeedbackController feedbackController;

        void Awake()
        {
            feedbackController = new FeedbackController(audioSystem, popupManager, popupData);
        }

        void OnEnable()
        {
            EventBus.Subscribe<OnHazardFixed>(OnHazardFixed);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<OnHazardFixed>(OnHazardFixed);
        }

        private void OnHazardFixed(OnHazardFixed evt)
        {
            feedbackController.FeedbackRequest(
                evt.soundDefinition,
                evt.hazardTitle,
                evt.description,
                true,
                evt.hazardID
            );
        }
    }

    public class FeedbackController
    {
        private readonly AudioSystem audioSystem;
        private readonly PopupManager popupManager;
        private readonly PopupData popupTemplate;
        private readonly Queue<Feedbacker> queue;
        private bool isRunning;
        private const float extraWaitTime = 0.2f;

        private CancellationTokenSource currentCts;
        private Feedbacker? currentItem;
        private AudioVoice currentVoice;

        public FeedbackController(AudioSystem audioSystem, PopupManager popupManager, PopupData popupTemplate)
        {
            this.audioSystem = audioSystem;
            this.popupManager = popupManager;
            this.popupTemplate = popupTemplate;

            queue = new Queue<Feedbacker>();
        }

        public void FeedbackRequest(SoundDefinition soundDefinition, string title, string message, bool isHazard, int hazardID)
        {
            float duration = GetDuration(soundDefinition, hazardID - 1);
            var newFeedback = new Feedbacker(soundDefinition, title, message, isHazard, duration, hazardID);

            Debug.Log($"[FeedbackManager] New request - {title}, isRunning: {isRunning}, current queue: {queue.Count}");

            // Her yeni request'te PopupManager'ı sıfırla
            popupManager.CloseAllPopups();

            // Eğer şu anda bir şey oynatılıyorsa interrupt et
            if (currentItem.HasValue)
            {
                Debug.Log("[FeedbackManager] Interrupting current item");
                InterruptCurrent();
            }

            queue.Enqueue(newFeedback);
            Debug.Log($"[FeedbackManager] Item enqueued. New queue count: {queue.Count}");

            // Loop başlat
            if (!isRunning)
            {
                Debug.Log("[FeedbackManager] Starting queue loop");
                RunQueueLoop().Forget();
            }
        }

        private void InterruptCurrent()
        {
            // Cancel token'ı iptal et
            currentCts?.Cancel();
            currentCts?.Dispose();
            currentCts = null;

            // Sesi durdur
            if (currentVoice != null)
            {
                currentVoice.Stop();
                currentVoice = null;
            }
            
            // Mevcut item'ı queue'nun başına geri ekle
            if (currentItem.HasValue)
            {
                var temp = new Queue<Feedbacker>();
                temp.Enqueue(currentItem.Value);
                
                while (queue.Count > 0)
                {
                    temp.Enqueue(queue.Dequeue());
                }

                queue.Clear();
                while (temp.Count > 0)
                {
                    queue.Enqueue(temp.Dequeue());
                }

                currentItem = null;
            }
        }

        private float GetDuration(SoundDefinition soundDefinition, int indis)
        {
            if (soundDefinition != null &&
                soundDefinition.clips != null &&
                soundDefinition.clips.Count > 0 &&
                indis >= 0 &&
                indis < soundDefinition.clips.Count &&
                soundDefinition.clips[indis].clip != null)
            {
                return soundDefinition.clips[indis].clip.length;
            }

            return 1f;
        }

        private void SetPopupData(string title, string message, bool isHazard, float displayDuration)
        {
            popupTemplate.title = title;
            popupTemplate.message = message;
            popupTemplate.isHazard = isHazard;
            popupTemplate.displayDuration = displayDuration;
            popupTemplate.autoClose = false; // Manuel kontrol
        }

        private async UniTaskVoid RunQueueLoop()
        {
            if (isRunning)
            {
                Debug.LogWarning("[FeedbackManager] RunQueueLoop called but already running!");
                return;
            }

            Debug.Log("[FeedbackManager] ===== Queue loop STARTED =====");
            isRunning = true;

            try
            {
                while (queue.Count > 0)
                {
                    Debug.Log($"[FeedbackManager] Processing item. Queue count: {queue.Count}");
                    
                    var item = queue.Dequeue();
                    currentItem = item;
                    currentCts = new CancellationTokenSource();
                    
                    try
                    {
                        await PlayOne(item, currentCts.Token);
                        
                        // Normal completion - popup'ı kapat
                        Debug.Log("[FeedbackManager] Item completed normally, closing popup");
                        popupManager.CloseCurrentPopup();
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log("[FeedbackManager] Item was cancelled/interrupted");
                    }
                    finally
                    {
                        currentItem = null;
                        currentCts?.Dispose();
                        currentCts = null;
                    }
                    
                    // Küçük bir delay
                    await UniTask.Delay(100);
                }
            }
            finally
            {
                Debug.Log($"[FeedbackManager] ===== Queue loop FINISHED ===== Remaining: {queue.Count}");
                isRunning = false;
            }
        }

        private async UniTask PlayOne(Feedbacker item, CancellationToken ct)
        {
            Debug.Log($"[FeedbackManager] >>> Playing: {item.title} (duration: {item.duration}s)");
            
            SetPopupData(item.title, item.message, item.isHazard, item.duration);

            var ctx = PlayContext.Default;
            ctx.SetClipIndex(item.hazardID - 1);
            ctx.ignoreCooldowns = true;

            // Popup ve ses aynı anda başlat
            popupManager.EnqueuePopup(popupTemplate);
            currentVoice = audioSystem.Play(item.sound, ctx);

            if (currentVoice == null)
            {
                Debug.LogWarning("[FeedbackManager] AudioVoice is null!");
                popupManager.CloseCurrentPopup();
                return;
            }

            int gen = currentVoice.Generation;
            
            // Ses bitene kadar bekle
            await WaitVoiceCompletion(currentVoice, gen, item.duration + extraWaitTime, ct);
            
            Debug.Log($"[FeedbackManager] <<< Finished: {item.title}");
            currentVoice = null;
        }

        private UniTask WaitVoiceCompletion(AudioVoice voice, int generation, float timeoutSeconds, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource();

            void Handler(int gen)
            {
                if (gen != generation) return;
                Debug.Log($"[FeedbackManager] Voice completed (gen: {gen})");
                tcs.TrySetResult();
            }

            voice.OnCompleted += Handler;

            if (voice.LastCompletedGeneration == generation)
            {
                tcs.TrySetResult();
            }

            return AwaitAndUnsub(voice, Handler, tcs.Task, timeoutSeconds, ct);

            static async UniTask AwaitAndUnsub(AudioVoice v, System.Action<int> h, UniTask task, float timeoutSeconds, CancellationToken ct)
            {
                try
                {
                    var result = await UniTask.WhenAny(
                        task,
                        UniTask.Delay(System.TimeSpan.FromSeconds(timeoutSeconds), cancellationToken: ct)
                    );
                    
                    if (result == 1)
                    {
                        Debug.LogWarning($"[FeedbackManager] Voice TIMEOUT after {timeoutSeconds}s");
                    }
                }
                finally
                {
                    if (v != null) v.OnCompleted -= h;
                }
            }
        }
    }

    public readonly struct Feedbacker
    {
        public readonly SoundDefinition sound;
        public readonly string title;
        public readonly string message;
        public readonly bool isHazard;
        public readonly float duration;
        public readonly int hazardID;

        public Feedbacker(SoundDefinition sound, string title, string message, bool isHazard, float duration, int hazardID)
        {
            this.sound = sound;
            this.title = title;
            this.message = message;
            this.isHazard = isHazard;
            this.duration = duration;
            this.hazardID = hazardID;
        }
    }
}