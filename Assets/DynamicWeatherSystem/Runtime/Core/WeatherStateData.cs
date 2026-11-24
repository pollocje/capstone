using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Defines all visual and audio parameters that characterize a single weather state.
    /// Create via: Assets > Create > Dynamic Weather System > Weather State
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeatherState_New",
        menuName = "Dynamic Weather System/Weather State",
        order = 0)]
    public class WeatherStateData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Descriptive name for this state. Used in logs and the Manager inspector.")]
        public string stateName = "New State";

        [Header("Fog")]
        [Tooltip("Enables or disables global scene fog when this state is active.")]
        public bool fogEnabled = false;

        [Tooltip("Fog color.")]
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("Fog density in Exponential Squared mode. Values above 0.05 are very dense.")]
        [Range(0f, 0.1f)]
        public float fogDensity = 0.02f;

        [Header("Directional Light")]
        [Tooltip("Color of the main directional light (sun or moon).")]
        public Color lightColor = Color.white;

        [Tooltip("Intensity of the main directional light.")]
        [Range(0f, 8f)]
        public float lightIntensity = 1f;

        [Header("Ambient Light")]
        [Tooltip("Scene ambient light color.")]
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f, 1f);

        [Header("Sky")]
        [Tooltip("Multiplicative tint applied to the skybox material. " +
                 "White = no change. Darken for storms, shift cool for rain.")]
        public Color skyboxTint = Color.white;

        [Tooltip("Skybox exposure (overall brightness). " +
                 "1 = normal. >1 = brighter. <1 = darker.")]
        [Range(0f, 8f)]
        public float skyboxExposure = 1f;

        [Header("Audio")]
        [Tooltip("Looping ambient audio clip for this state. " +
                 "Can be rain, wind, forest ambience, etc. " +
                 "Leave empty for silence.")]
        public AudioClip ambientClip;

        [Tooltip("Target volume for the ambient clip in this state.")]
        [Range(0f, 1f)]
        public float ambientVolume = 0.5f;

        [Header("Rain")]
        [Tooltip("Rain intensity. 0 = no rain, 1 = maximum rain. " +
                 "Transitions between states interpolate this value continuously.")]
        [Range(0f, 1f)]
        public float rainIntensity = 0f;

        [Tooltip("Color and opacity of the rain drops.")]
        public Color rainColor = new Color(0.7f, 0.8f, 0.9f, 0.4f);

        [Tooltip("Downward fall speed of rain drops in metres per second.")]
        [Range(2f, 20f)]
        public float rainSpeed = 8f;
    }
}
