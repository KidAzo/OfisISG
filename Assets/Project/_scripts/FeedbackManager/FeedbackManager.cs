using UnityEngine;
using WoiUtils.AudioSystem;
using Woi.PopUpSystem;
using System.Collections.Generic;
using Reflex.Attributes;
using Cysharp.Threading.Tasks;
using Woi.Events;

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
        private const float beforeTheVoice = 0.2f;

        public FeedbackController(AudioSystem audioSystem, PopupManager popupManager, PopupData popupTemplate)
        {
            this.audioSystem = audioSystem;
            this.popupManager = popupManager;
            this.popupTemplate = popupTemplate;

            queue = new Queue<Feedbacker>();
        }

        //PUBLIC APIs
        public void FeedbackRequest(SoundDefinition soundDefinition, string title, string message, bool isHazard, int hazardID)
        {
            float duration = GetDuration(soundDefinition, hazardID - 1);
            queue.Enqueue(new Feedbacker(soundDefinition, title, message, isHazard, duration, hazardID));

            if (!isRunning)
                RunQueueLoop().Forget();
        }

        private float GetDuration(SoundDefinition soundDefinition, int indis)
        {
            if (soundDefinition != null &&
                soundDefinition.clips != null &&
                soundDefinition.clips.Count > 0 &&
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
        }

        private async UniTaskVoid RunQueueLoop()
        {
            Debug.Log("FeedbackManager: Starting queue loop.");
            isRunning = true;

            try
            {
                while (queue.Count > 0)
                {
                    Debug.Log("FeedbackManager: Processing next item in queue.");
                    var item = queue.Dequeue();
                    await PlayOne(item);
                }
            }
            finally
            {
                isRunning = false;
            }
        }

        private async UniTask PlayOne(Feedbacker item)
        {
            SetPopupData(item.title, item.message, item.isHazard, item.duration);

            popupManager.EnqueuePopup(popupTemplate);

            AudioVoice voice = null;

            var ctx = PlayContext.Default;
            ctx.SetClipIndex(item.hazardID - 1);
            ctx.ignoreCooldowns = true;

            await UniTask.Delay(beforeTheVoice); 
            
            voice = audioSystem.Play(
                item.sound,
                ctx
            );

            if (voice == null)
                return;

            int gen = voice.Generation;
            await WaitVoiceCompletion(voice, gen, item.duration + extraWaitTime);
        }

        private UniTask WaitVoiceCompletion(AudioVoice voice, int generation, float timeoutSeconds)
        {
            var tcs = new UniTaskCompletionSource();

            void Handler(int gen)
            {
                if (gen != generation) return;
                tcs.TrySetResult();
            }

            voice.OnCompleted += Handler;

            if (voice.LastCompletedGeneration == generation)
                tcs.TrySetResult();

            return AwaitAndUnsub(voice, Handler, tcs.Task, timeoutSeconds);

            static async UniTask AwaitAndUnsub(AudioVoice v, System.Action<int> h, UniTask task, float timeoutSeconds)
            {
                try
                {
                    await UniTask.WhenAny(
                        task,
                        UniTask.Delay(timeoutSeconds)
                    );
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
