using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Woi.Game.Training
{
    /// <summary>
    /// Live sweep metrics panel. Defaults to a <b>Screen Space Overlay</b> uGUI layer so it stays visible
    /// above UI Toolkit (IMGUI <see cref="OnGUI"/> is often drawn underneath fullscreen UIDocuments).
    /// </summary>
    /// <remarks>
    /// <b>Placement:</b> Put this on an <b>always-active</b> GameObject (e.g. same object as
    /// <see cref="ExtinguisherSessionRecorder"/>). Do <b>not</b> parent it under
    /// <c>TrainingResultScreenSessionBinder</c>'s results root — that hierarchy is often
    /// <see cref="GameObject.SetActive"/> disabled while the session runs. The uGUI overlay is
    /// parented under <see cref="TrainingSweepDebugOverlayHost"/> (DontDestroyOnLoad) so it stays
    /// live; refresh is driven from the host even when this GameObject is inactive.
    /// <para />
    /// Toggle <see cref="_useScreenSpaceOverlay"/> off to use legacy IMGUI only (e.g. if no UI Toolkit
    /// covers the Game view).
    /// <para />
    /// <b>Editor only:</b> no overlay or IMGUI path runs in standalone players (<see cref="Application.isEditor"/>).
    /// </remarks>
    [AddComponentMenu("WOI/Training/Spray Sweep Debug HUD")]
    [DefaultExecutionOrder(32000)]
    public sealed class TrainingSpraySweepDebugHud : MonoBehaviour
    {
        public enum HudAnchor
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }

        [SerializeField] private ExtinguisherSessionRecorder _recorder;

        [Tooltip("If the assigned recorder never gets BeginSession (duplicate in scene), pick the recorder that actually has IsSessionActive.")]
        [SerializeField] private bool _autoPickRecorderWithActiveSession = true;

        [SerializeField] private bool _show = false;

        [Tooltip("When on, draws via uGUI Canvas (sorting order 32767) so the panel appears above UI Toolkit. When off, uses legacy IMGUI only.")]
        [SerializeField] private bool _useScreenSpaceOverlay = true;

        [Tooltip("Panel corner relative to the screen safe area.")]
        [SerializeField] private HudAnchor _anchor = HudAnchor.TopRight;

        [SerializeField] private Vector2 _margin = new Vector2(12f, 12f);

        [SerializeField, Min(200f)] private float _width = 320f;

        private GUIStyle _box;
        private GUIStyle _label;
        private GUIStyle _labelWarn;
        private bool _stylesReady;

        private float _nextResolveTime;
        private ExtinguisherSessionRecorder _cachedResolved;

        private Canvas _overlayCanvas;
        private RectTransform _overlayPanel;
        private Text _overlayText;
        private readonly StringBuilder _textBuilder = new StringBuilder(512);

        private void Reset()
        {
            _recorder = GetComponent<ExtinguisherSessionRecorder>();
        }

        public void SetVisible(bool visible) => _show = visible;

        /// <summary>Recorder used for display: assigned, or (when enabled) any in-scene recorder with an active session.</summary>
        private ExtinguisherSessionRecorder ResolveRecorder()
        {
            if (!_autoPickRecorderWithActiveSession)
                return _recorder;

            // Never throttle the assigned recorder once it has an active session — the old 0.25s cache
            // could keep returning a stale instance right after BeginSession, so the HUD looked "dead".
            if (_recorder != null && _recorder.isActiveAndEnabled && _recorder.IsSessionActive)
            {
                _cachedResolved = _recorder;
                return _cachedResolved;
            }

            float t = Time.unscaledTime;
            if (t < _nextResolveTime
                && _cachedResolved != null
                && _cachedResolved.isActiveAndEnabled
                && _cachedResolved.IsSessionActive)
            {
                return _cachedResolved;
            }

            _nextResolveTime = t + 0.25f;

            ExtinguisherSessionRecorder[] all = FindObjectsByType<ExtinguisherSessionRecorder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                ExtinguisherSessionRecorder r = all[i];
                if (r != null && r.isActiveAndEnabled && r.IsSessionActive)
                {
                    _cachedResolved = r;
                    return _cachedResolved;
                }
            }

            _cachedResolved = _recorder;
            return _cachedResolved;
        }

        private void OnEnable()
        {
            _cachedResolved = null;
            _nextResolveTime = 0f;

            if (!gameObject.activeInHierarchy)
                return;

            if (Application.isPlaying && Application.isEditor && _useScreenSpaceOverlay)
                TrainingSweepDebugOverlayHost.Instance.Register(this);

            // If this component lives under a disabled parent, OnGUI never runs — common mis-setup.
            Transform p = transform.parent;
            while (p != null)
            {
                if (!p.gameObject.activeSelf)
                {
                    Debug.LogWarning(
                        $"[{nameof(TrainingSpraySweepDebugHud)}] Parent '{p.name}' is inactive — " +
                        $"IMGUI mode would not draw here; screen overlay still works via {nameof(TrainingSweepDebugOverlayHost)}.",
                        this);
                    break;
                }

                p = p.parent;
            }

            if (_useScreenSpaceOverlay && Application.isEditor)
                EnsureOverlay();
        }

        private void OnDisable()
        {
            // Parent was disabled (e.g. results root during session) — keep registered so the host keeps refreshing.
            if (Application.isPlaying && !gameObject.activeInHierarchy)
                return;

            if (Application.isPlaying)
                TrainingSweepDebugOverlayHost.Instance.Unregister(this);

            TeardownOverlay();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
                TrainingSweepDebugOverlayHost.Instance.Unregister(this);

            TeardownOverlay();
        }

        /// <summary>Called every frame from <see cref="TrainingSweepDebugOverlayHost"/> (works while this object is inactive).</summary>
        internal void RefreshOverlayFromHost()
        {
            if (!_useScreenSpaceOverlay || !Application.isEditor)
                return;

            EnsureOverlay();
            if (_overlayPanel == null || _overlayText == null)
                return;

            RefreshScreenOverlayCore();
        }

        private void RefreshScreenOverlayCore()
        {
            if (!Application.isEditor || !_show)
            {
                if (_overlayPanel != null)
                    _overlayPanel.gameObject.SetActive(false);
                return;
            }

            _overlayPanel.gameObject.SetActive(true);
            ComposeHudPlainText(_textBuilder);
            _overlayText.text = _textBuilder.ToString();
            Canvas.ForceUpdateCanvases();

            Rect safe = Screen.safeArea;
            float w = Mathf.Min(_width, safe.width - 16f);
            float textWidth = w - 20f;
            float textHeight = _overlayText.cachedTextGeneratorForLayout.GetPreferredHeight(
                _overlayText.text,
                _overlayText.GetGenerationSettings(new Vector2(textWidth, 0f)));
            float panelH = Mathf.Max(textHeight + 24f, 120f);

            _overlayPanel.sizeDelta = new Vector2(w, panelH);

            bool leftSide = _anchor == HudAnchor.TopLeft || _anchor == HudAnchor.BottomLeft;
            bool topSide = _anchor == HudAnchor.TopLeft || _anchor == HudAnchor.TopRight;
            _overlayPanel.pivot = new Vector2(leftSide ? 0f : 1f, topSide ? 1f : 0f);
            _overlayPanel.anchorMin = _overlayPanel.anchorMax = new Vector2(leftSide ? 0f : 1f, topSide ? 1f : 0f);

            float insetRight = Screen.width - safe.xMax;
            float insetTop = Screen.height - safe.yMax;
            float posX = leftSide ? safe.x + _margin.x : -(insetRight + _margin.x);
            float posY = topSide ? -(insetTop + _margin.y) : safe.y + _margin.y;
            _overlayPanel.anchoredPosition = new Vector2(posX, posY);
        }

        private void EnsureOverlay()
        {
            if (_overlayCanvas != null)
                return;

            var root = new GameObject($"{nameof(TrainingSpraySweepDebugHud)}_Overlay_{GetInstanceID()}");
            int uiLayer = LayerMask.NameToLayer("UI");
            root.layer = uiLayer >= 0 ? uiLayer : gameObject.layer;

            // Overlay canvases parented under a plain Transform often end up with a broken rect / scale and draw nothing.
            // Root + DontDestroyOnLoad matches normal UI Toolkit / uGUI setup.
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(root);
            }
            else
            {
                root.transform.SetParent(transform, false);
            }

            _overlayCanvas = root.AddComponent<Canvas>();
            var rootRt = root.GetComponent<RectTransform>();
            // Root canvas is always full-screen; Unity also drives this rect — never put a fullscreen Image here.
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;

            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.pixelPerfect = false;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 32767;

            // No CanvasScaler: with Screen Space Overlay at hierarchy root, layout units track screen pixels reliably.

            var cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Plain GameObjects do not always get a RectTransform immediately when parented; adding a Graphic
            // on the panel guarantees a RectTransform on this Unity version.
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(root.transform, false);
            var panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);
            panelBg.raycastTarget = false;
            _overlayPanel = panelGo.GetComponent<RectTransform>();
            _overlayPanel.localScale = Vector3.one;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            _overlayText = textGo.AddComponent<Text>();
            RectTransform textRt = _overlayText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(10f, 10f);
            textRt.offsetMax = new Vector2(-10f, -10f);

            _overlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_overlayText.font == null)
                _overlayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _overlayText.fontSize = 13;
            _overlayText.color = new Color(0.93f, 0.94f, 0.96f);
            _overlayText.supportRichText = true;
            _overlayText.alignment = TextAnchor.UpperLeft;
            _overlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _overlayText.verticalOverflow = VerticalWrapMode.Overflow;
            _overlayText.raycastTarget = false;
        }

        private void TeardownOverlay()
        {
            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas.gameObject);
                _overlayCanvas = null;
                _overlayPanel = null;
                _overlayText = null;
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !Application.isEditor)
                return;
            if (_useScreenSpaceOverlay)
                EnsureOverlay();
            else
            {
                TrainingSweepDebugOverlayHost.Instance.Unregister(this);
                TeardownOverlay();
            }
        }

        /// <summary>Plain text + rich tags (Unity UI Text) lines, newline separated.</summary>
        private void ComposeHudPlainText(StringBuilder sb)
        {
            sb.Clear();

            ExtinguisherSessionRecorder active = ResolveRecorder();

            if (active == null)
            {
                sb.AppendLine("<b>Spray sweep debug</b>");
                sb.AppendLine("No <b>Extinguisher Session Recorder</b> in scene. Assign one on this component.");
                sb.AppendLine("Keep this object active during Play (not under disabled results UI).");
                return;
            }

            SpraySweepSettings s = active.SweepMonitorSettings;

            if (!active.IsSessionActive)
            {
                int n = CountRecordersInScene();
                sb.AppendLine("<b>Spray sweep (training)</b>");
                sb.AppendLine("<color=#FFB86B>No active session</color> on the resolved recorder. Call <b>BeginSession</b> on the same ExtinguisherSessionRecorder your scenario uses.");
                sb.AppendLine($"Recorder: <b>{active.gameObject.name}</b>  |  In scene: {n} instance(s)");
                if (n > 1)
                    sb.AppendLine("Multiple recorders: enable <b>Auto Pick Recorder With Active Session</b> or assign the object that receives BeginSession.");
                sb.AppendLine(
                    $"Thresholds: {s.RollingWindowSeconds:F1}s window, {s.MinimumSampleCount} samples, {s.MinimumHorizontalSpanMeters:F2}m span, " +
                    $"performed time spread ≥{s.MinimumPerformedTemporalSpreadSeconds:F2}s, min sustain {s.MinimumSweepDurationSeconds:F2}s, " +
                    $"quality time spread ≥{s.MinimumTemporalSpreadFraction:P0} window, base-only={s.BaseZoneHitsOnly}");
                return;
            }

            if (_recorder != null && active != _recorder)
                sb.AppendLine($"<color=#8EC5FF>Session on '{active.gameObject.name}'</color> (assigned '{_recorder.gameObject.name}' had no active session).");

            active.GetSweepRuntimeDebug(
                out int samples,
                out float span,
                out bool performedSession,
                out bool performedLive,
                out bool rulePassed,
                out float peak,
                out float streakSec,
                out int validHits,
                out int baseHits);

            float needSpan = s.MinimumHorizontalSpanMeters;
            int needN = s.MinimumSampleCount;

            string status = rulePassed
                ? "Training rule satisfied (live)"
                : performedLive
                    ? "This window meets loose motion (span + time spread)"
                    : samples >= needN && span < needSpan
                        ? "Span below threshold"
                        : samples < needN
                            ? "Collecting samples…"
                            : "Need wider span and/or samples spread over time";

            sb.AppendLine("<b>Spray sweep (training)</b>");
            sb.AppendLine($"Recorder: <b>{active.gameObject.name}</b>  |  Session: active");
            sb.AppendLine($"Window: {s.RollingWindowSeconds:F1}s  |  Min samples: {needN}  |  Min span: {needSpan:F2}m  |  Loose time spread: {s.MinimumPerformedTemporalSpreadSeconds:F2}s+");
            sb.AppendLine($"Base-only: {(s.BaseZoneHitsOnly ? "Yes" : "No")}");
            sb.AppendLine("Session active: Yes");
            sb.AppendLine($"Zone hits (session): {validHits}  |  Base-zone hits: {baseHits}");
            if (s.BaseZoneHitsOnly && validHits > 0 && baseHits == 0)
                sb.AppendLine("<color=#FFB86B>Sweep window uses base hits only — spray the <b>base</b> zone or disable Base-only in recorder settings.</color>");
            sb.AppendLine($"Samples in window: {samples}");
            sb.AppendLine($"Current span (XZ): {span:F2} m");
            sb.AppendLine($"Peak span (session): {peak:F2} m");
            sb.AppendLine($"Quality streak (max): {streakSec:F2} s");
            sb.AppendLine(performedLive ? "<color=#8EC5FF>Loose motion (this window): Yes</color>" : "Loose motion (this window): No");
            sb.AppendLine(performedSession ? "<color=#AAAAAA>Loose motion latched this session: Yes</color>" : "Loose motion latched this session: No");
            sb.AppendLine(rulePassed ? "<color=#7AE582>Training rule passed: Yes</color>" : "Training rule passed: No");
            sb.AppendLine($"Status: {status}");
        }

        private void OnGUI()
        {
            if (!Application.isEditor || _useScreenSpaceOverlay || !_show)
                return;

            // Draw IMGUI as late / “on top” as the legacy system allows (UI Toolkit may still composite later).
            GUI.depth = -10000;
            EnsureStyles();

            Rect area = ComputePanelRect(320f);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.94f);
            GUI.Box(area, string.Empty, _box);
            GUI.backgroundColor = prev;

            float x = area.x + 10f;
            float y = area.y + 8f;
            float line = 22f;
            float textW = area.width - 20f;

            void Row(string text, GUIStyle st)
            {
                float h = st.CalcHeight(new GUIContent(text), textW);
                h = Mathf.Max(h, line);
                GUI.Label(new Rect(x, y, textW, h), text, st);
                y += h + 2f;
            }

            ComposeHudPlainText(_textBuilder);
            string[] lines = _textBuilder.ToString().Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string rowText = lines[i];
                if (string.IsNullOrEmpty(rowText))
                    continue;
                bool warn = rowText.IndexOf("Multiple recorders", StringComparison.Ordinal) >= 0
                    || rowText.IndexOf("Session on '", StringComparison.Ordinal) >= 0;
                Row(rowText, warn ? _labelWarn : _label);
            }
        }

        private Rect ComputePanelRect(float minHeight)
        {
            Rect safe = Screen.safeArea;
            float w = Mathf.Min(_width, safe.width - 16f);
            float h = minHeight;

            float left = safe.x + _margin.x;
            float right = safe.xMax - w - _margin.x;
            float top = safe.y + _margin.y;
            float bottom = safe.yMax - h - _margin.y;

            bool leftSide = _anchor == HudAnchor.TopLeft || _anchor == HudAnchor.BottomLeft;
            bool topSide = _anchor == HudAnchor.TopLeft || _anchor == HudAnchor.TopRight;
            float x = leftSide ? left : right;
            float y = topSide ? top : bottom;

            x = Mathf.Clamp(x, safe.x + 4f, safe.xMax - w - 4f);
            y = Mathf.Clamp(y, safe.y + 4f, safe.yMax - h - 4f);

            return new Rect(x, y, w, h);
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
                return;

            _box = new GUIStyle(GUI.skin.box)
            {
                richText = true,
                padding = new RectOffset(8, 8, 8, 8),
                border = new RectOffset(6, 6, 6, 6),
            };

            _label = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.93f, 0.94f, 0.96f) },
            };

            _labelWarn = new GUIStyle(_label)
            {
                normal = { textColor = new Color(1f, 0.72f, 0.42f) },
            };

            _stylesReady = true;
        }

        private static int CountRecordersInScene()
        {
            return FindObjectsByType<ExtinguisherSessionRecorder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        }
    }

    /// <summary>
    /// Holds spray-sweep debug <see cref="Canvas"/> children and refreshes them even when the
    /// <see cref="TrainingSpraySweepDebugHud"/> lives under a GameObject disabled for the session.
    /// </summary>
    [DefaultExecutionOrder(32001)]
    internal sealed class TrainingSweepDebugOverlayHost : MonoBehaviour
    {
        static TrainingSweepDebugOverlayHost _instance;
        readonly List<TrainingSpraySweepDebugHud> _huds = new List<TrainingSpraySweepDebugHud>(2);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        internal static TrainingSweepDebugOverlayHost Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[WOI] Training Sweep Debug Overlay Host");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<TrainingSweepDebugOverlayHost>();
                }

                return _instance;
            }
        }

        internal void Register(TrainingSpraySweepDebugHud hud)
        {
            if (hud == null || _huds.Contains(hud))
                return;
            _huds.Add(hud);
        }

        internal void Unregister(TrainingSpraySweepDebugHud hud)
        {
            if (hud == null)
                return;
            _huds.Remove(hud);
        }

        private void LateUpdate()
        {
            for (int i = _huds.Count - 1; i >= 0; i--)
            {
                TrainingSpraySweepDebugHud hud = _huds[i];
                if (hud == null)
                {
                    _huds.RemoveAt(i);
                    continue;
                }

                hud.RefreshOverlayFromHost();
            }
        }
    }
}
