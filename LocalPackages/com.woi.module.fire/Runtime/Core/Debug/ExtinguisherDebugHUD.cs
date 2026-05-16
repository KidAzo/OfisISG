using UnityEngine;

namespace FireExtinguisher.Core
{
    /// <summary>
    /// Optional debug component that renders a lightweight on-screen IMGUI panel
    /// showing live extinguisher state and the last <see cref="ExtinguishResult"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach to any active GameObject in the scene. Does not need to be on the
    /// extinguisher itself. Assign the <see cref="ExtinguisherController"/> and
    /// optionally the <see cref="IAimProvider"/> source.
    /// </para>
    /// <para>
    /// The panel is rendered using Unity's built-in IMGUI — no UI Toolkit or Canvas
    /// required. Toggle it at any time via the <see cref="_showHUD"/> field or by
    /// calling <see cref="SetVisible"/>.
    /// </para>
    /// <para>
    /// Renders only in the Unity Editor (<see cref="Application.isEditor"/>); hidden in all standalone players.
    /// </para>
    /// <para>
    /// Safe to disable or remove — nothing in the core system depends on this component.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Fire Extinguisher/Debug/Extinguisher Debug HUD")]
    public sealed class ExtinguisherDebugHUD : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The ExtinguisherController to monitor.")]
        [SerializeField] private ExtinguisherController _controller;

        [Tooltip("MonoBehaviour that implements IAimProvider — used to display aim validity. " +
                 "Optional; leave empty to omit that row.")]
        [SerializeField] private MonoBehaviour _aimProviderSource;

        [Header("Display")]
        [Tooltip("Toggle the HUD in the Editor only; never shown in standalone builds.")]
        [SerializeField] private bool _showHUD = false;

        [Tooltip("Screen-space top-left corner of the panel in pixels.")]
        [SerializeField] private Vector2 _panelPosition = new Vector2(10f, 10f);

        [Tooltip("Width of the panel in pixels.")]
        [SerializeField, Min(100f)] private float _panelWidth = 280f;

        // ── Colours (not Inspector-exposed — constants for a debug tool are fine) ─

        private static readonly Color ColorGood    = new Color(0.4f, 1.0f, 0.4f);
        private static readonly Color ColorBad     = new Color(1.0f, 0.4f, 0.4f);
        private static readonly Color ColorWarn    = new Color(1.0f, 0.85f, 0.2f);
        private static readonly Color ColorNeutral = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color ColorTitle   = new Color(0.6f, 0.9f, 1.0f);

        // ── Runtime state ─────────────────────────────────────────────────────────

        private IAimProvider     _aimProvider;
        private ExtinguishResult _lastResult;
        private bool             _hasResult;
        private GUIStyle         _boxStyle;
        private GUIStyle         _labelStyle;
        private bool             _stylesInitialised;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _aimProvider = _aimProviderSource as IAimProvider;

            // Fall back to any IAimProvider on this same GameObject.
            if (_aimProvider == null)
                _aimProvider = GetComponent<IAimProvider>();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnSprayEvaluated    += HandleSprayEvaluated;
                _controller.OnSprayStopped      += HandleSprayStopped;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnSprayEvaluated    -= HandleSprayEvaluated;
                _controller.OnSprayStopped      -= HandleSprayStopped;
            }
        }

        private void OnGUI()
        {
            if (!Application.isEditor || !_showHUD || _controller == null) return;

            EnsureStyles();

            float panelHeight = EstimatePanelHeight();
            Rect  panelRect   = new Rect(_panelPosition.x, _panelPosition.y, _panelWidth, panelHeight);

            GUI.Box(panelRect, GUIContent.none, _boxStyle);
            GUILayout.BeginArea(panelRect);
            GUILayout.Space(4f);

            DrawTitle("Fire Extinguisher Debug");
            DrawDivider();

            DrawControllerSection();

            if (_aimProvider != null)
                DrawAimSection();

            DrawDivider();
            DrawResultSection();

            GUILayout.Space(4f);
            GUILayout.EndArea();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Shows or hides the HUD panel without disabling the component.</summary>
        public void SetVisible(bool visible) => _showHUD = visible;

        /// <summary>Toggles the HUD panel visibility.</summary>
        public void ToggleVisible() => _showHUD = !_showHUD;

        // ── Section drawing ───────────────────────────────────────────────────────

        private void DrawControllerSection()
        {
            ExtinguisherData data = _controller.ExtinguisherData;

            DrawRow("Type",
                data != null ? data.ExtinguisherType.ToString() : "—",
                ColorNeutral);

            DrawRow("Held",
                _controller.IsDischarging || IsHeldFromProvider() ? "Yes" : "No",
                IsHeldFromProvider() ? ColorGood : ColorBad);

            DrawRow("Discharging",
                _controller.IsDischarging ? "YES" : "No",
                _controller.IsDischarging ? ColorGood : ColorNeutral);

            DrawRow("Depleted",
                _controller.IsDepleted ? "DEPLETED" : "No",
                _controller.IsDepleted ? ColorBad : ColorGood);

            // Capacity bar.
            float norm = _controller.NormalizedCapacity;
            Color capColor = norm > 0.5f ? ColorGood : norm > 0.2f ? ColorWarn : ColorBad;
            DrawRow("Capacity",
                $"{norm * 100f:F1}%  ({_controller.RemainingCapacity:F1} / {_controller.MaxCapacity:F1})",
                capColor);
        }

        private void DrawAimSection()
        {
            bool valid = _aimProvider.IsAimValid;
            DrawRow("Aim Valid",   valid ? "Yes" : "NO",          valid ? ColorGood : ColorBad);
            DrawRow("Aim Point",   FormatVector(_aimProvider.AimPoint),    ColorNeutral);
            DrawRow("Spray origin",
                FormatVector(_controller != null
                    ? _controller.ResolvedSprayWorldOrigin
                    : _aimProvider.SprayOrigin),
                ColorNeutral);
        }

        private void DrawResultSection()
        {
            DrawTitle("Last Spray Result");

            if (!_hasResult)
            {
                DrawColored("  Not sprayed yet.", ColorNeutral);
                return;
            }

            if (!_lastResult.DidHitZone)
            {
                DrawRow("Hit",        "MISS",                     ColorBad);
                DrawRow("Miss Reason", _lastResult.MissReason.ToString(), ColorWarn);
                if (_lastResult.Distance > 0f)
                    DrawRow("Distance", $"{_lastResult.Distance:F2} m", ColorNeutral);
                return;
            }

            DrawRow("Hit",         "YES",                                              ColorGood);
            DrawRow("Zone",        _lastResult.HitZone != null ? _lastResult.HitZone.name : "null", ColorNeutral);
            DrawRow("Fire Source", _lastResult.Source  != null ? _lastResult.Source.name  : "null", ColorNeutral);
            DrawRow("Distance",    $"{_lastResult.Distance:F2} m",                     ColorNeutral);
            DrawRow("Angle",       $"{_lastResult.AngleFromCenter:F1}°",               ColorNeutral);
            DrawRow("Coverage",    $"{_lastResult.CoverageScore:F2}",                  ScoreColor(_lastResult.CoverageScore));
            DrawRow("Dist Score",  $"{_lastResult.DistanceScore:F2}",                  ScoreColor(_lastResult.DistanceScore));

            Color compatColor = _lastResult.Compatibility switch
            {
                CompatibilityResult.Effective => ColorGood,
                CompatibilityResult.Forbidden => ColorBad,
                _                             => ColorWarn,
            };
            DrawRow("Compat",   _lastResult.Compatibility.ToString(), compatColor);
            DrawRow("Amount",   $"{_lastResult.ExtinguishAmountCalculated:F5}",        ColorNeutral);

            if (_lastResult.Source != null)
            {
                DrawDivider();
                DrawRow("Fire State",  _lastResult.Source.State.ToString(),
                    _lastResult.Source.IsExtinguished ? ColorGood : ColorWarn);
                DrawRow("Fire Intensity",
                    $"{_lastResult.Source.CurrentNormalizedIntensity * 100f:F1}%",
                    ScoreColor(1f - _lastResult.Source.CurrentNormalizedIntensity));

                DrawZonesSection(_lastResult.Source, _lastResult.HitZone);
            }
        }

        private void DrawZonesSection(FireSource source, FireTargetZone hitZone)
        {
            var zones = source.Zones;
            if (zones == null || zones.Count == 0) return;

            DrawDivider();
            DrawTitle($"Zones  ({zones.Count})");

            for (int i = 0; i < zones.Count; i++)
            {
                FireTargetZone zone = zones[i];
                if (zone == null) continue;

                bool isHit        = zone == hitZone;
                bool isOut        = zone.IsExtinguished;
                float intensity   = zone.NormalizedIntensity;

                // Label: highlight the currently hit zone in cyan, out zones in grey.
                Color nameColor = isHit ? ColorTitle : isOut ? ColorNeutral * 0.6f : ColorNeutral;

                string hitTag  = isHit ? " ◄HIT" : "";
                string outTag  = isOut ? " OUT"  : "";
                string typeTag = zone.ZoneType.ToString();

                GUILayout.BeginHorizontal();

                // Zone index + name
                GUI.color = nameColor;
                GUILayout.Label($"  [{i}] {zone.name}", _labelStyle,
                    GUILayout.Width(_panelWidth * 0.42f));

                // Type
                GUI.color = ColorNeutral * 0.8f;
                GUILayout.Label(typeTag, _labelStyle,
                    GUILayout.Width(_panelWidth * 0.2f));

                // Intensity % + absolute value
                Color intColor = isOut ? ColorNeutral * 0.5f : ScoreColor(1f - intensity);
                GUI.color = intColor;
                string intLabel = isOut
                    ? "OUT"
                    : $"{intensity * 100f:F0}%  ({zone.CurrentIntensity:F2})";
                GUILayout.Label(intLabel, _labelStyle,
                    GUILayout.Width(_panelWidth * 0.32f));

                // Hit tag
                GUI.color = isHit ? ColorTitle : Color.clear;
                GUILayout.Label(hitTag + outTag, _labelStyle);

                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }
        }

        // ── IMGUI helpers ─────────────────────────────────────────────────────────

        private void DrawTitle(string text)
        {
            GUI.color = ColorTitle;
            GUILayout.Label(text, _labelStyle);
            GUI.color = Color.white;
        }

        private void DrawDivider()
        {
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUILayout.Label("────────────────────────", _labelStyle);
            GUI.color = Color.white;
        }

        private void DrawRow(string label, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUI.color = ColorNeutral;
            GUILayout.Label($"  {label,-14}", _labelStyle, GUILayout.Width(_panelWidth * 0.45f));
            GUI.color = valueColor;
            GUILayout.Label(value, _labelStyle);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawColored(string text, Color color)
        {
            GUI.color = color;
            GUILayout.Label(text, _labelStyle);
            GUI.color = Color.white;
        }

        // ── Style initialisation ──────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_stylesInitialised) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeSolidTexture(new Color(0.05f, 0.05f, 0.1f, 0.82f)) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                richText  = false,
                wordWrap  = false,
                padding   = new RectOffset(2, 2, 1, 1),
            };

            _stylesInitialised = true;
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        // ── Utility ───────────────────────────────────────────────────────────────

        private float EstimatePanelHeight()
        {
            // Rough estimate: each row is ~16px; add padding per section.
            int rows = 7;                      // controller rows
            if (_aimProvider != null) rows += 3;

            if (!_hasResult)
            {
                rows += 1;
            }
            else if (!_lastResult.DidHitZone)
            {
                rows += 4;
            }
            else
            {
                rows += 10; // hit result rows (title + 9 data rows)
                rows += 3;  // divider + Fire State + Fire Intensity
                int zoneCount = _lastResult.Source != null && _lastResult.Source.Zones != null
                    ? _lastResult.Source.Zones.Count
                    : 0;
                rows += 2 + zoneCount; // divider + zones title + one row per zone
            }

            return rows * 16f + 40f;
        }

        private bool IsHeldFromProvider()
        {
            // The controller doesn't expose IsHeld publicly, so we derive it
            // from whether the extinguisher is active and can discharge.
            return !_controller.IsDepleted && _controller.enabled;
        }

        private static string FormatVector(Vector3 v) =>
            $"({v.x:F1}, {v.y:F1}, {v.z:F1})";

        private static Color ScoreColor(float score) =>
            score >= 0.75f ? ColorGood : score >= 0.4f ? ColorWarn : ColorBad;

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleSprayEvaluated(ExtinguishResult result)
        {
            _lastResult = result;
            _hasResult  = true;
        }

        private void HandleSprayStopped()
        {
            // Keep the last result visible after stopping so the panel
            // shows what the final tick produced.
        }
    }
}
