using UnityEngine;
using UnityEditor;
using System.IO;

namespace DynamicWeatherSystem.Editor
{
    /// <summary>
    /// Creates the four sample weather presets (Clear, Rain, Fog, Storm)
    /// as ready-to-use WeatherStateData assets.
    ///
    /// Use via: Assets > Create > Dynamic Weather System > Create Sample Presets
    /// Assets are created in the folder currently selected in the Project window.
    /// </summary>
    public static class WeatherPresetsCreator
    {
        private const string MenuPath =
            "Assets/Create/Dynamic Weather System/Create Sample Presets";

        [MenuItem(MenuPath)]
        public static void CreateAllPresets()
        {
            string folder = GetSelectedFolder();

            CreatePreset(folder, BuildClear());
            CreatePreset(folder, BuildRain());
            CreatePreset(folder, BuildFog());
            CreatePreset(folder, BuildStorm());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DynamicWeatherSystem] 4 presets created in: " + folder);
        }

        // --- Preset Builders ---

        private static WeatherStateData BuildClear()
        {
            var d = ScriptableObject.CreateInstance<WeatherStateData>();
            d.name           = "WS_Clear";
            d.stateName      = "Clear";
            // Light
            d.lightColor     = new Color(1.00f, 0.95f, 0.82f);
            d.lightIntensity = 1.3f;
            d.ambientColor   = new Color(0.25f, 0.28f, 0.35f);
            // Fog
            d.fogEnabled     = false;
            d.fogColor       = new Color(0.80f, 0.85f, 0.90f);
            d.fogDensity     = 0.001f;
            // Rain
            d.rainIntensity  = 0f;
            d.rainColor      = new Color(0.70f, 0.80f, 0.90f, 0.35f);
            d.rainSpeed      = 8f;
            // Sky
            d.skyboxTint     = new Color(1.00f, 0.98f, 0.92f);
            d.skyboxExposure = 1.3f;
            // Audio: no clip (silence or optional light ambience)
            d.ambientClip    = null;
            d.ambientVolume  = 0f;
            return d;
        }

        private static WeatherStateData BuildRain()
        {
            var d = ScriptableObject.CreateInstance<WeatherStateData>();
            d.name           = "WS_Rain";
            d.stateName      = "Rain";
            // Light
            d.lightColor     = new Color(0.72f, 0.78f, 0.88f);
            d.lightIntensity = 0.6f;
            d.ambientColor   = new Color(0.14f, 0.17f, 0.22f);
            // Fog
            d.fogEnabled     = true;
            d.fogColor       = new Color(0.55f, 0.58f, 0.63f);
            d.fogDensity     = 0.015f;
            // Rain
            d.rainIntensity  = 0.6f;
            d.rainColor      = new Color(0.70f, 0.80f, 0.90f, 0.35f);
            d.rainSpeed      = 9f;
            // Sky
            d.skyboxTint     = new Color(0.68f, 0.72f, 0.80f);
            d.skyboxExposure = 0.65f;
            // Audio: assign a soft rain loop clip in the Inspector
            d.ambientClip    = null;
            d.ambientVolume  = 0.55f;
            return d;
        }

        private static WeatherStateData BuildFog()
        {
            var d = ScriptableObject.CreateInstance<WeatherStateData>();
            d.name           = "WS_Fog";
            d.stateName      = "Fog";
            // Light
            d.lightColor     = new Color(0.88f, 0.86f, 0.80f);
            d.lightIntensity = 0.45f;
            d.ambientColor   = new Color(0.28f, 0.28f, 0.30f);
            // Fog
            d.fogEnabled     = true;
            d.fogColor       = new Color(0.74f, 0.74f, 0.72f);
            d.fogDensity     = 0.05f;
            // Rain
            d.rainIntensity  = 0f;
            d.rainColor      = new Color(0.70f, 0.80f, 0.90f, 0.35f);
            d.rainSpeed      = 8f;
            // Sky
            d.skyboxTint     = new Color(0.82f, 0.82f, 0.80f);
            d.skyboxExposure = 0.5f;
            // Audio: assign a soft wind or muted ambient clip in the Inspector
            d.ambientClip    = null;
            d.ambientVolume  = 0.30f;
            return d;
        }

        private static WeatherStateData BuildStorm()
        {
            var d = ScriptableObject.CreateInstance<WeatherStateData>();
            d.name           = "WS_Storm";
            d.stateName      = "Storm";
            // Light
            d.lightColor     = new Color(0.50f, 0.55f, 0.68f);
            d.lightIntensity = 0.25f;
            d.ambientColor   = new Color(0.07f, 0.08f, 0.12f);
            // Fog
            d.fogEnabled     = true;
            d.fogColor       = new Color(0.28f, 0.30f, 0.38f);
            d.fogDensity     = 0.022f;
            // Rain
            d.rainIntensity  = 0.95f;
            d.rainColor      = new Color(0.60f, 0.68f, 0.80f, 0.45f);
            d.rainSpeed      = 14f;
            // Sky
            d.skyboxTint     = new Color(0.38f, 0.40f, 0.50f);
            d.skyboxExposure = 0.22f;
            // Audio: assign a storm clip (heavy rain + wind) in the Inspector
            d.ambientClip    = null;
            d.ambientVolume  = 0.75f;
            return d;
        }

        // --- Utilities ---

        private static void CreatePreset(string folder, WeatherStateData data)
        {
            string path = Path.Combine(folder, data.name + ".asset")
                              .Replace("\\", "/");

            // If the asset already exists, update its values instead of duplicating it
            var existing = AssetDatabase.LoadAssetAtPath<WeatherStateData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(data, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(data);
                Debug.Log($"[DynamicWeatherSystem] Preset updated: {path}");
            }
            else
            {
                AssetDatabase.CreateAsset(data, path);
                Debug.Log($"[DynamicWeatherSystem] Preset created: {path}");
            }
        }

        private static string GetSelectedFolder()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(path))
                return "Assets";

            if (!AssetDatabase.IsValidFolder(path))
                path = Path.GetDirectoryName(path);

            return path.Replace("\\", "/");
        }
    }
}
