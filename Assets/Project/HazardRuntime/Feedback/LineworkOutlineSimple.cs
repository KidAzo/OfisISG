using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Woi.Feedbacks
{
    public class TriggerOutlineLayer : MonoBehaviour
    {
        [SerializeField] private string outlineLayerName = "Outline";
        [SerializeField] private string defaultLayerName = "Default";

        int outlineLayer;
        int defaultLayer;

        CancellationTokenSource cts;
        bool _missingLayerLogged;

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
                if (!_missingLayerLogged)
                {
                    _missingLayerLogged = true;
                    Debug.LogError($"Layer bulunamadı! Outline: {outlineLayer}, Default: {defaultLayer}");
                }
                return;
            }

            cts?.Cancel();
            cts = new CancellationTokenSource();

            // TÜM child'ların da layer'ını değiştir
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            int rootLayerBefore = gameObject.layer;
            int rendererLayerBefore = renderers.Length > 0 ? renderers[0].gameObject.layer : -1;
            SetLayerRecursively(gameObject, outlineLayer);

            Renderer firstRenderer = renderers.Length > 0 ? renderers[0] : null;
            string rendererPath = firstRenderer != null ? GetHierarchyPath(firstRenderer.transform) : "none";
            int rendererLayerAfter = firstRenderer != null ? firstRenderer.gameObject.layer : -1;
            string rendererRenderingLayer = firstRenderer != null ? $"0x{firstRenderer.renderingLayerMask:X8}" : "none";
            bool allRendererLayersMatch = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.layer == outlineLayer)
                    continue;

                allRendererLayersMatch = false;
                break;
            }

            Debug.Log(
                $"[HazardOutline] object={GetHierarchyPath(transform)} renderer={rendererPath} " +
                $"rootLayer={rootLayerBefore}->{gameObject.layer} rendererLayer={rendererLayerBefore}->{rendererLayerAfter} " +
                $"rendererRenderingLayer={rendererRenderingLayer} " +
                $"childRendererLayers={(allRendererLayersMatch ? "all-match" : "mismatch")} rendererCount={renderers.Length} " +
                $"triggerCalled=true featureExpectedLayer={outlineLayerName}({outlineLayer})",
                this);

            DisableLater(seconds, cts.Token).Forget();
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }

            return path;
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
