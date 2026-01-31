using UnityEngine;
using WoiUtils.AudioSystem;
using Woi.PopUpSystem;
using System.Collections.Generic;
using Reflex.Attributes;
using Cysharp.Threading.Tasks;

namespace Woi.FeedbackManager
{
    public class FeedbackManager : MonoBehaviour
    {
        [Inject] private AudioSystem audioSystem;
        [Inject] private PopupManager popupManager;
        [SerializeField] private PopupData popupData;
    }

    public class FeedbackController
    {
        private readonly AudioSystem audioSystem;
        private readonly PopupManager popupManager;
        private readonly PopupData popupTemplate;
        private readonly Queue<Feedbacker> queue;
        private bool isRunning;

        public FeedbackController(AudioSystem audioSystem, PopupManager popupManager, PopupData popupTemplate)
        {
            this.audioSystem = audioSystem;
            this.popupManager = popupManager;
            this.popupTemplate = popupTemplate;

            queue = new Queue<Feedbacker>();
        }

        //PUBLIC APIs
        public void FeedbackRequest(SoundDefinition soundDefinition, string title, string message, bool isHazard)
        {
            float duration = GetDuration(soundDefinition);
            queue.Enqueue(new Feedbacker(soundDefinition, title, message, isHazard, duration));

            if (!isRunning)
                RunQueueLoop().Forget();
        }

        private float GetDuration(SoundDefinition soundDefinition)
        {
            // senin logic: ilk clip uzunluğu, yoksa 1s
            if (soundDefinition != null &&
                soundDefinition.clips != null &&
                soundDefinition.clips.Count > 0 &&
                soundDefinition.clips[0].clip != null)
            {
                return soundDefinition.clips[0].clip.length;
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
            isRunning = true;

            try
            {
                while (queue.Count > 0)
                {
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

            popupManager.CreateInfoPopup(popupTemplate, item.isHazard);

            var voice = audioSystem.Play(item.sound);
            if (voice == null)
            {
                // ses yoksa fallback: duration kadar bekle
                await UniTask.Delay(System.TimeSpan.FromSeconds(item.duration));
                return;
            }

            int gen = voice.Generation;

            // opsiyonel: loop ise asla bitmez -> fallback
            if (voice.Data != null && voice.Data.loop)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(item.duration));
                return;
            }

            await WaitVoiceCompletion(voice, gen);
        }

        private UniTask WaitVoiceCompletion(AudioVoice voice, int generation)
        {
            var tcs = new UniTaskCompletionSource();

            void Handler(int gen)
            {
                if (gen != generation) return;
                tcs.TrySetResult();
            }

            voice.OnCompleted += Handler;

            return AwaitAndUnsub(voice, Handler, tcs.Task);

            static async UniTask AwaitAndUnsub(AudioVoice v, System.Action<int> h, UniTask task)
            {
                try { await task; }
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

        public Feedbacker(SoundDefinition sound, string title, string message, bool isHazard, float duration)
        {
            this.sound = sound;
            this.title = title;
            this.message = message;
            this.isHazard = isHazard;
            this.duration = duration;
        }
    }
}
