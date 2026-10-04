using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Abstract base class for all Dynamic Weather System modules.
    ///
    /// To create a custom module:
    ///   1. Create a class that inherits from WeatherModule.
    ///   2. Implement the Blend() method.
    ///   3. Add the component as a child of the GameObject that holds WeatherManager.
    ///
    /// WeatherManager automatically discovers all child modules on Awake.
    /// </summary>
    public abstract class WeatherModule : MonoBehaviour
    {
        /// <summary>
        /// Applies a state immediately, with no interpolation.
        /// No need to override this — it delegates to Blend() internally.
        /// </summary>
        public void Apply(WeatherStateData state) => Blend(state, state, 1f);

        /// <summary>
        /// Interpolates between two weather states using the normalized value t [0, 1].
        /// t = 0 → pure "from" state.
        /// t = 1 → pure "to" state.
        /// This method is called every frame during an active transition.
        /// </summary>
        public abstract void Blend(WeatherStateData from, WeatherStateData to, float t);
    }
}
