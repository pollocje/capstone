using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Module that controls global scene fog via RenderSettings.
    /// Compatible with URP without any additional dependencies.
    ///
    /// Fog-during-transition behaviour:
    ///   - If the source state has fog and the target does not (or vice versa),
    ///     fog remains active throughout the entire transition and is disabled
    ///     only when the target state is fully reached. Density fades on its own.
    ///
    /// Note: This module can be extended in the future to control a URP Volume
    ///       Override for volumetric fog.
    /// </summary>
    [AddComponentMenu("Dynamic Weather System/Modules/Fog Module")]
    public class FogModule : WeatherModule
    {
        public override void Blend(WeatherStateData from, WeatherStateData to, float t)
        {
            // Keep fog active while transitioning if either state has fog enabled,
            // so density can fade smoothly without a sudden pop.
            // At t = 1, Apply() calls Blend(to, to, 1) and writes the correct
            // fogEnabled value from the target state.
            bool fogActive = t < 1f
                ? (from.fogEnabled || to.fogEnabled)
                : to.fogEnabled;

            RenderSettings.fog        = fogActive;
            RenderSettings.fogMode    = FogMode.ExponentialSquared;
            RenderSettings.fogColor   = Color.Lerp(from.fogColor,   to.fogColor,   t);
            RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t);
        }
    }
}
