using UnityEngine;
using UnityEditor;

namespace DynamicWeatherSystem.Editor
{
    /// <summary>
    /// Custom inspector for WeatherManager.
    /// Allows selecting, previewing, and applying weather states in Play Mode
    /// with or without a transition — no code required.
    /// </summary>
    [CustomEditor(typeof(WeatherManager))]
    public class WeatherManagerEditor : UnityEditor.Editor
    {
        private WeatherStateData[] _availableStates;
        private string[] _stateNames;
        private int _selectedIndex;
        private float _customDuration = 2f;

        private void OnEnable()
        {
            RefreshStateList();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            DrawSectionHeader("Play Mode Preview");

            if (_availableStates == null || _availableStates.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No WeatherStateData assets found in the project.\n" +
                    "Create one via: Assets > Create > Dynamic Weather System > Weather State",
                    MessageType.Info);

                DrawRefreshButton();
                return;
            }

            var manager = (WeatherManager)target;

            // State selector dropdown
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _availableStates.Length - 1);
            _selectedIndex = EditorGUILayout.Popup("Weather State", _selectedIndex, _stateNames);

            // Custom duration field — only visible in Play Mode
            if (Application.isPlaying)
            {
                _customDuration = EditorGUILayout.FloatField(
                    new GUIContent("Duration (s)",
                        "Transition duration in seconds. 0 = immediate change."),
                    _customDuration);
                _customDuration = Mathf.Max(0f, _customDuration);
            }

            EditorGUILayout.Space(4);

            // Action buttons
            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply with Transition", GUILayout.Height(28)))
                        manager.SetWeather(_availableStates[_selectedIndex], _customDuration);

                    if (GUILayout.Button("Apply Immediate", GUILayout.Height(28)))
                        manager.SetWeather(_availableStates[_selectedIndex], 0f);
                }
            }

            EditorGUILayout.Space(6);

            // Runtime status display
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to apply states in real time.",
                    MessageType.None);
            }
            else
            {
                DrawRuntimeStatus(manager);
            }

            EditorGUILayout.Space(4);
            DrawRefreshButton();
        }

        private void DrawRuntimeStatus(WeatherManager manager)
        {
            if (manager.IsTransitioning)
            {
                EditorGUILayout.HelpBox(
                    $"Transitioning to: {manager.CurrentState?.stateName}",
                    MessageType.None);

                var rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(rect, manager.TransitionProgress,
                    $"{manager.TransitionProgress * 100f:F0}%");

                // Repaint every frame while a transition is active
                Repaint();
            }
            else if (manager.CurrentState != null)
            {
                EditorGUILayout.HelpBox(
                    $"Active state: {manager.CurrentState.stateName}",
                    MessageType.None);
            }
        }

        private void DrawSectionHeader(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(4);
        }

        private void DrawRefreshButton()
        {
            if (GUILayout.Button("Refresh State List"))
                RefreshStateList();
        }

        private void RefreshStateList()
        {
            var guids = AssetDatabase.FindAssets("t:WeatherStateData");

            _availableStates = new WeatherStateData[guids.Length];
            _stateNames      = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availableStates[i] = AssetDatabase.LoadAssetAtPath<WeatherStateData>(path);
                _stateNames[i] = string.IsNullOrEmpty(_availableStates[i].stateName)
                    ? _availableStates[i].name
                    : _availableStates[i].stateName;
            }
        }
    }
}
