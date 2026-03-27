using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;
using Woi.Events;

namespace Woi.Leaderboard
{
    /// <summary>
    /// VR World-Space Leaderboard Panel.
    ///
    /// Setup in Unity:
    ///  1. Create a RenderTexture asset (e.g. 760x900, R8G8B8A8).
    ///  2. Create a PanelSettings asset → set Target Texture to the RenderTexture.
    ///  3. Add a UIDocument to this GameObject → assign Leaderboard.uxml + the PanelSettings.
    ///  4. Create a child Quad (scale ~1.2 x 1.5 x 1) → assign a material with the RenderTexture.
    ///  5. Assign the LeaderboardUIController reference in the Inspector.
    ///  6. Place this GameObject in your VR scene. It starts hidden.
    ///  7. When OnLeaderboardUpdated fires it animates into view, then auto-hides.
    /// </summary>
    public class VRLeaderboardController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The LeaderboardUIController on the same UIDocument.")]
        [SerializeField] private LeaderboardUIController leaderboardUI;

        [Tooltip("The Quad (child) that displays the RenderTexture.")]
        [SerializeField] private Transform panelQuad;

        [Header("Position & Rotation")]
        [Tooltip("Offset from the VR camera when the panel appears.")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.2f, 2f);

        [Tooltip("Optional: if assigned, the panel faces this transform (usually VR camera).")]
        [SerializeField] private Transform lookAtTarget;

        [Header("Animation")]
        [SerializeField] private float showDuration  = 0.45f;
        [SerializeField] private float hideDuration  = 0.30f;
        [SerializeField] private Ease  showEase      = Ease.OutBack;
        [SerializeField] private Ease  hideEase      = Ease.InBack;

        [Header("Auto-hide")]
        [Tooltip("Seconds before the panel automatically hides. 0 = never auto-hide.")]
        [SerializeField] private float autoHideDelay = 20f;

        // ─── state ───────────────────────────────────────────
        private bool    _visible;
        private Tween   _activeTween;
        private Tween   _autoHideTween;
        private Vector3 _targetScale;   // Inspector scale — preserved, never overwritten

        private EventBus.Subscription _leaderboardSub;
        private EventBus.Subscription _escapeSub;

        // ─────────────────────────────────────────────────────
        void Awake()
        {
            // Snapshot the scale the designer set in the Inspector.
            _targetScale = panelQuad != null ? panelQuad.localScale : Vector3.one;
        }

        void OnEnable()
        {
            _leaderboardSub = EventBus.Subscribe<OnLeaderboardUpdated>(_ => Show());
            _escapeSub      = EventBus.Subscribe<OnEscapePerformed>(OnEscape);
        }

        void OnDisable()
        {
            _leaderboardSub?.Dispose();
            _escapeSub?.Dispose();
            _activeTween.Stop();
            _autoHideTween.Stop();
        }

        // ─────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────
        public void Show()
        {
            if (_visible) return;
            _visible = true;

            PositionPanel();
            leaderboardUI?.Refresh();

            _activeTween.Stop();
            _activeTween = Tween.Scale(panelQuad, endValue: _targetScale,
                duration: showDuration, ease: showEase);

            ScheduleAutoHide();
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;

            _autoHideTween.Stop();
            _activeTween.Stop();
            _activeTween = Tween.Scale(panelQuad, endValue: Vector3.zero,
                duration: hideDuration, ease: hideEase);
        }

        // ─────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────
        private void PositionPanel()
        {
            if (lookAtTarget == null) return;

            transform.position = lookAtTarget.position
                                 + lookAtTarget.TransformDirection(positionOffset);

            // Face the player
            Vector3 dir = transform.position - lookAtTarget.position;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void ScheduleAutoHide()
        {
            _autoHideTween.Stop();
            if (autoHideDelay > 0f)
                _autoHideTween = Tween.Delay(autoHideDelay, Hide);
        }

        private void OnEscape(OnEscapePerformed evt)
        {
            if (_visible && evt.state)
                Hide();
        }
    }
}
