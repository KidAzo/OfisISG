using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Woi.PopUpSystem
{
    public class OnGameStartTrigger : PopupTrigger
    {
        CancellationTokenSource cts;

        protected override void InitializeTrigger()
        {
            TriggerAfterDelay().Forget();
        }

        async UniTaskVoid TriggerAfterDelay()
        {
            cts = new CancellationTokenSource();

            try
            {
                float delay = popupDatas.Length > 0 ? popupDatas[0].delayTime : 0f;

                await UniTask.WaitForSeconds(delay, cancellationToken: cts.Token);
                TriggerPopup();
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}