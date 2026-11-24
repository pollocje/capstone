using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Module that controls the main Directional Light and the scene ambient light.
    ///
    /// Setup:
    ///   - Add this component as a child of the GameObject that holds WeatherManager.
    ///   - Assign the scene's Directional Light in the Inspector.
    ///   - If left unassigned, the module will attempt to find it automatically on Reset.
    /// </summary>
    [AddComponentMenu("Dynamic Weather System/Modules/Light Module")]
    public class LightModule : WeatherModule
    {
        [Tooltip("The scene's Directional Light representing the sun or moon.")]
        [SerializeField] private Light directionalLight;

        /// <summary>
        /// Attempts to auto-find the first Directional Light in the scene
        /// when the component is added or reset from the Inspector.
        /// </summary>
        private void Reset()
        {
            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    directionalLight = l;
                    break;
                }
            }
        }

        public override void Blend(WeatherStateData from, WeatherStateData to, float t)
        {
            BlendDirectionalLight(from, to, t);
            BlendAmbientLight(from, to, t);
        }

        private void BlendDirectionalLight(WeatherStateData from, WeatherStateData to, float t)
        {
            if (directionalLight == null)
            {
                Debug.LogWarning("[LightModule] No Directional Light assigned. " +
                    "Assign one in the component Inspector.", this);
                return;
            }

            directionalLight.color     = Color.Lerp(from.lightColor,     to.lightColor,     t);
            directionalLight.intensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
        }

        private void BlendAmbientLight(WeatherStateData from, WeatherStateData to, float t)
        {
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, t);
        }
    }
}
