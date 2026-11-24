using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Runtime demo UI for the Dynamic Weather System.
    ///
    /// Setup:
    ///   1. Add this component to any GameObject in the scene.
    ///   2. Assign the WeatherManager and the 4 presets in the Inspector.
    ///   3. Enter Play Mode and use the buttons to switch weather states.
    ///
    /// No Canvas or additional UI configuration required.
    /// </summary>
    public class WeatherDemoUI : MonoBehaviour
    {
        [Header("Weather System")]
        [Tooltip("Reference to the WeatherManager in the scene. Auto-found if left empty.")]
        [SerializeField] private WeatherManager weatherManager;

        [Header("Presets")]
        [SerializeField] private WeatherStateData presetClear;
        [SerializeField] private WeatherStateData presetRain;
        [SerializeField] private WeatherStateData presetFog;
        [SerializeField] private WeatherStateData presetStorm;

        [Header("Settings")]
        [Tooltip("Transition duration in seconds when a preset button is pressed.")]
        [SerializeField, Range(0.5f, 12f)] private float transitionDuration = 3f;

        // Styles — initialised on the first OnGUI call to avoid errors outside that context
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonActiveStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _sublabelStyle;
        private bool     _stylesReady;

        private static readonly Color C_Clear = new Color(1.00f, 0.84f, 0.25f);
        private static readonly Color C_Rain  = new Color(0.28f, 0.60f, 1.00f);
        private static readonly Color C_Fog   = new Color(0.64f, 0.68f, 0.76f);
        private static readonly Color C_Storm = new Color(0.45f, 0.28f, 0.72f);

        private void Awake()
        {
            if (weatherManager == null)
                weatherManager = FindAnyObjectByType<WeatherManager>();
        }

        private void OnGUI()
        {
            InitStyles();

            const float W      = 264f;
            const float margin = 20f;

            // Exact sum of all layout elements
            float panelH = 28      // title
                         + 10      // Space(10)
                         + 1       // separator
                         + 10      // Space(10)
                         + 4 * 40  // 4 buttons × 40px
                         + 3 * 6   // 3 gaps between buttons × 6px
                         + 14      // Space(14)
                         + 1       // separator
                         + 14      // Space(14)
                         + 20      // duration row
                         + 14      // Space(14)
                         + 1       // separator
                         + 14      // Space(14)
                         + 20      // state label
                         + 4       // Space(4) before progress bar
                         + 7       // progress bar GetRect(7f)
                         + margin * 2   // top + bottom padding
                         + 30;     // safety buffer

            // Top-left corner
            var panelRect = new Rect(margin, margin, W, panelH);

            // Panel background
            var savedColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.05f, 0.06f, 0.13f, 0.92f);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);
            GUI.backgroundColor = savedColor;

            GUILayout.BeginArea(new Rect(
                panelRect.x + margin,
                panelRect.y + margin,
                W - margin * 2,
                panelH - margin * 2));

            // Title
            GUILayout.Label("Dynamic Weather System", _titleStyle);
            GUILayout.Space(10f);
            DrawSeparator(W - margin * 2);
            GUILayout.Space(10f);

            // Preset buttons
            DrawPresetButton("Clear",  presetClear,  C_Clear);
            GUILayout.Space(6f);
            DrawPresetButton("Rain",   presetRain,   C_Rain);
            GUILayout.Space(6f);
            DrawPresetButton("Fog",    presetFog,    C_Fog);
            GUILayout.Space(6f);
            DrawPresetButton("Storm",  presetStorm,  C_Storm);

            GUILayout.Space(14f);
            DrawSeparator(W - margin * 2);
            GUILayout.Space(14f);

            // Transition duration control
            DrawDurationRow(W - margin * 2);

            GUILayout.Space(14f);
            DrawSeparator(W - margin * 2);
            GUILayout.Space(14f);

            // State label and progress bar
            DrawStateDisplay(W - margin * 2);

            GUILayout.EndArea();
        }

        // --- Sections ---

        private void DrawPresetButton(string label, WeatherStateData preset, Color accent)
        {
            if (preset == null) return;

            bool isActive = weatherManager != null && weatherManager.CurrentState == preset;

            var style = isActive ? _buttonActiveStyle : _buttonStyle;
            var saved = GUI.backgroundColor;
            GUI.backgroundColor = isActive
                ? new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 0.95f)
                : new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 0.90f);

            if (GUILayout.Button(label, style, GUILayout.Height(40f)))
                weatherManager?.SetWeather(preset, transitionDuration);

            GUI.backgroundColor = saved;
        }

        private void DrawDurationRow(float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Transition", _sublabelStyle, GUILayout.Width(76f));
            transitionDuration = GUILayout.HorizontalSlider(
                transitionDuration, 0.5f, 12f, GUILayout.Width(width - 76f - 46f));
            GUILayout.Label($"{transitionDuration:F1}s", _labelStyle, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
        }

        private void DrawStateDisplay(float width)
        {
            if (weatherManager == null) return;

            bool transitioning = weatherManager.IsTransitioning;
            var  current       = weatherManager.CurrentState;

            string stateText = transitioning
                ? $"\u2192  {current?.stateName ?? "?"}"
                : (current != null ? current.stateName : "\u2014");

            var saved = GUI.contentColor;
            GUI.contentColor = transitioning
                ? new Color(0.28f, 0.68f, 1f)
                : new Color(0.48f, 0.54f, 0.65f);
            GUILayout.Label(stateText, _sublabelStyle);
            GUI.contentColor = saved;

            // Progress bar — always visible
            GUILayout.Space(4f);
            var barRect = GUILayoutUtility.GetRect(width, 7f);

            float fill = transitioning ? weatherManager.TransitionProgress : 0f;

            var bgSaved = GUI.color;

            // Background
            GUI.color = new Color(0.10f, 0.12f, 0.20f, 1f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            // Fill (only when there is progress to show)
            if (fill > 0f)
            {
                GUI.color = new Color(0.28f, 0.68f, 1f, 1f);
                GUI.DrawTexture(
                    new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height),
                    Texture2D.whiteTexture);
            }

            GUI.color = bgSaved;
        }

        private static void DrawSeparator(float width)
        {
            var rect  = GUILayoutUtility.GetRect(width, 1f);
            var saved = GUI.color;
            GUI.color = new Color(0.22f, 0.26f, 0.36f, 0.70f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        // --- Styles ---

        private void InitStyles()
        {
            if (_stylesReady) return;

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakePixel(new Color(0.05f, 0.06f, 0.13f, 0.92f)) }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.70f, 0.82f, 1.00f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(14, 8, 0, 0),
                normal    = { textColor = new Color(0.86f, 0.89f, 0.94f) },
                hover     = { textColor = Color.white },
                active    = { textColor = Color.white }
            };

            _buttonActiveStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = Color.white },
                hover  = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = new Color(0.86f, 0.89f, 0.94f) }
            };

            _sublabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.48f, 0.54f, 0.65f) }
            };

            _stylesReady = true;
        }

        private static Texture2D MakePixel(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
