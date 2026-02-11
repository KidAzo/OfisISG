using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;


namespace Woi.Feedbacks
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public class TriggerOutlineLayer : MonoBehaviour
    {
        [SerializeField] private string outlineLayerName = "Outline";
        [SerializeField] private string defaultLayerName = "Default";

        int outlineLayer;
        int defaultLayer;

        CancellationTokenSource cts;

        void Awake()
        {
            outlineLayer = LayerMask.NameToLayer(outlineLayerName);
            defaultLayer = LayerMask.NameToLayer(defaultLayerName);
        }

        /// <summary>
        /// Objeyi outline layer'a alır, süre dolunca geri alır
        /// </summary>
        public void Trigger(float seconds)
        {
            if (outlineLayer < 0 || defaultLayer < 0)
            {
                Debug.LogError($"Layer bulunamadı! Outline: {outlineLayer}, Default: {defaultLayer}");
                return;
            }

            cts?.Cancel();
            cts = new CancellationTokenSource();

            // TÜM child'ların da layer'ını değiştir
            SetLayerRecursively(gameObject, outlineLayer);
            Debug.Log($"Triggered outline for {gameObject.name} and all children to layer {outlineLayer}");

            DisableLater(seconds, cts.Token).Forget();
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        async UniTaskVoid DisableLater(float seconds, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
                SetLayerRecursively(gameObject, defaultLayer);
            }
            catch (OperationCanceledException) { }
        }

        void OnDisable()
        {
            cts?.Cancel();
            SetLayerRecursively(gameObject, defaultLayer);
        }
    }
    }
