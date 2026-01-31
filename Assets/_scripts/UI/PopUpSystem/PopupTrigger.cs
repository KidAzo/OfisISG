using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using Reflex.Attributes;

namespace Woi.PopUpSystem
{
    public class PopupTrigger : MonoBehaviour
    {
        [SerializeField] protected PopupData[] popupDatas; 
        [SerializeField] protected bool showSequentially = true;
      
        [Inject] protected PopupManager popupManager;

        protected BasePopup currentPopup;
        protected bool hasTriggered = false;
      
        CancellationTokenSource cts;
        
        protected virtual void Start()
        {
            InitializeTrigger();
        }

        protected virtual void InitializeTrigger() {}

        public virtual void TriggerPopup()
        {
            if (hasTriggered) return;
            hasTriggered = true;

            if (popupDatas == null || popupDatas.Length == 0) return;

            foreach (var data in popupDatas)
            {
                popupManager.EnqueuePopup(data);
            }
        }

        protected async UniTaskVoid ShowPopupsSequentially()
        {
            cts = new CancellationTokenSource();

            try
            {
                foreach (var data in popupDatas)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    currentPopup = popupManager.CreateInfoPopup(data, data.isHazard);
                    currentPopup.gameObject.SetActive(true);
                    currentPopup.Show();

                    if (data.autoClose)
                    {
                        await UniTask.WaitForSeconds(data.displayDuration, cancellationToken: cts.Token);
                        currentPopup?.Hide();
                    }
                    else
                    {
                        // Manuel close ise kullanıcıyı bekle
                        await WaitForUserClose(cts.Token);
                    }

                    // Popup'lar arası küçük bekleme (opsiyonel)
                    await UniTask.WaitForSeconds(data.delayTime, cancellationToken: cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancel edildi
            }
        }

        protected async UniTask WaitForUserClose(CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource();

            // Popup kapatıldığında signal ver
            Action onClose = () => completionSource.TrySetResult();
            
            currentPopup.OnConfirm(onClose);
            currentPopup.OnCancel(onClose);

            await completionSource.Task;
        }

        public virtual void ResetTrigger()
        {
            hasTriggered = false;
        }
        
        protected virtual void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
