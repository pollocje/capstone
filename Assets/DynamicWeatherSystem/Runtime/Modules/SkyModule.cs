using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Module that controls the tint and exposure of the active scene skybox.
    ///
    /// Setup:
    ///   - Add this component as a child of the GameObject that holds WeatherManager.
    ///   - Make sure the scene has a skybox assigned in Lighting > Environment.
    ///   - Set skyboxTint and skyboxExposure values in each WeatherStateData.
    ///
    /// Compatibility:
    ///   - Unity Procedural Skybox  → _SkyTint / _Exposure properties
    ///   - 6-Sided / Panoramic      → _Tint / _Exposure properties
    ///   - Any custom shader that exposes _Tint or _SkyTint and _Exposure will work.
    ///
    /// Note: The module creates a runtime instance of the skybox material to avoid
    ///       modifying the original asset on disk. The original is restored on destroy.
    /// </summary>
    [AddComponentMenu("Dynamic Weather System/Modules/Sky Module")]
    public class SkyModule : WeatherModule
    {
        // Cached shader property IDs to avoid per-frame string lookups
        private static readonly int TintId     = Shader.PropertyToID("_Tint");
        private static readonly int SkyTintId  = Shader.PropertyToID("_SkyTint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        private Material _originalSkybox;
        private Material _skyboxInstance;

        // Which tint property the current material exposes
        private int  _activeTintPropertyId;
        private bool _hasTint;
        private bool _hasExposure;
        private bool _isReady;

        // --- Lifecycle ---

        private void Awake()
        {
            var skybox = RenderSettings.skybox;

            if (skybox == null)
            {
                Debug.LogWarning("[SkyModule] No skybox is assigned in " +
                    "Lighting > Environment. The module will remain inactive.", this);
                return;
            }

            // Create an instance so the original asset is never modified
            _originalSkybox       = skybox;
            _skyboxInstance       = new Material(skybox) { name = skybox.name + "_Runtime" };
            RenderSettings.skybox = _skyboxInstance;

            DetectProperties();
            _isReady = true;
        }

        private void OnDestroy()
        {
            // Restore the original skybox when exiting Play Mode or destroying the GameObject
            if (_originalSkybox != null)
                RenderSettings.skybox = _originalSkybox;

            if (_skyboxInstance != null)
                Destroy(_skyboxInstance);
        }

        // --- WeatherModule ---

        public override void Blend(WeatherStateData from, WeatherStateData to, float t)
        {
            if (!_isReady) return;

            Color tint     = Color.Lerp(from.skyboxTint,     to.skyboxTint,     t);
            float exposure = Mathf.Lerp(from.skyboxExposure, to.skyboxExposure, t);

            ApplySky(tint, exposure);
        }

        // --- Internal Logic ---

        private void ApplySky(Color tint, float exposure)
        {
            if (_hasTint)
                _skyboxInstance.SetColor(_activeTintPropertyId, tint);

            if (_hasExposure)
                _skyboxInstance.SetFloat(ExposureId, exposure);
        }

        /// <summary>
        /// Detects which properties are exposed by the current skybox material
        /// so we can control them without throwing console errors.
        /// </summary>
        private void DetectProperties()
        {
            // 6-Sided and Panoramic skyboxes use _Tint
            if (_skyboxInstance.HasProperty(TintId))
            {
                _hasTint = true;
                _activeTintPropertyId = TintId;
            }
            // Procedural Skybox uses _SkyTint
            else if (_skyboxInstance.HasProperty(SkyTintId))
            {
                _hasTint = true;
                _activeTintPropertyId = SkyTintId;
            }
            else
            {
                Debug.LogWarning("[SkyModule] The current skybox does not expose '_Tint' or '_SkyTint'. " +
                    "Tint blending will have no effect. Exposure control is still active.", this);
            }

            _hasExposure = _skyboxInstance.HasProperty(ExposureId);

            if (!_hasExposure)
            {
                Debug.LogWarning("[SkyModule] The current skybox does not expose '_Exposure'. " +
                    "Exposure blending will have no effect.", this);
            }
        }
    }
}
