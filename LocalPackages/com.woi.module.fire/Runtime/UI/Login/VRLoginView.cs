using System;
using UnityEngine;
using Woi.Events;

namespace Woi.UI.Navigation
{
    /// <summary>
    /// Lightweight <see cref="ILoginView"/> for VR; wire the actual UI + RenderTexture on the same object using
    /// <c>Woi.Fire.LoginScreenVr.VRLoginDocumentMount</c> + <see cref="LoginScreenController"/> (<c>omitUserProfileSection</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VRLoginView : MonoBehaviour, ILoginView
    {
        [SerializeField] RenderTexture panelRenderTexture;

        public event Action<string, string> OnLoginRequested;
        public event Action<OnLogged> OnFireModuleLoginCompleted;

        public void SetVisible(bool visible)
        {
            if (enabled != visible)
                enabled = visible;
        }

        public void SetLoading(bool loading)
        {
            Debug.Log($"[VRLoginView] SetLoading({loading}) — placeholder.", this);
        }

        public void ShowError(string message)
        {
            Debug.LogWarning($"[VRLoginView] ShowError: {message}", this);
        }

        public void ClearError()
        {
        }

        /// <summary>Optional hook if a mount drives the RT; otherwise assign <see cref="panelRenderTexture"/> in the Inspector.</summary>
        public void BindToRenderTexturePanel(RenderTexture renderTexture)
        {
            panelRenderTexture = renderTexture;
        }

        /// <summary>Extension: invoke when a VR-specific submit action occurs.</summary>
        public void HandleVRSubmit()
        {
            OnLoginRequested?.Invoke(string.Empty, string.Empty);
        }

        /// <summary>UV in RenderTexture texel space when <paramref name="worldRay"/> hits the quad used by <c>VRLoginDocumentMount</c>.</summary>
        public Vector2 ConvertWorldHitToPanelPosition(Ray worldRay, out bool hitPanel)
        {
            hitPanel = false;
            if (panelRenderTexture == null)
                return Vector2.zero;

            if (!Physics.Raycast(worldRay, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                return Vector2.zero;

            if (hit.collider == null || hit.collider.GetComponent<MeshRenderer>() == null)
                return Vector2.zero;

            var rend = hit.collider.GetComponent<MeshRenderer>();
            if (rend.sharedMaterial == null ||
                (rend.sharedMaterial.mainTexture != panelRenderTexture &&
                 (!rend.sharedMaterial.HasProperty("_BaseMap") || rend.sharedMaterial.GetTexture("_BaseMap") != panelRenderTexture)))
                return Vector2.zero;

            Vector2 uv = hit.textureCoord;
            uv.y = 1f - uv.y;
            hitPanel = true;
            return new Vector2(uv.x * panelRenderTexture.width, uv.y * panelRenderTexture.height);
        }
    }
}
