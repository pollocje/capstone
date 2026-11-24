using System.Collections;
using UnityEngine;

namespace DynamicWeatherSystem
{
    /// <summary>
    /// Central orchestrator for the Dynamic Weather System.
    ///
    /// Basic usage:
    ///   - Add this component to a root GameObject (or drag the WeatherSystem prefab into the scene).
    ///   - Assign an initial state in the Inspector.
    ///   - Call SetWeather(state) to change the weather using the default transition duration.
    ///   - Call SetWeather(state, duration) to specify a custom duration in seconds.
    ///   - Call SetWeather(state, 0f) for an immediate change with no transition.
    ///
    /// All modules (LightModule, FogModule, etc.) must be children of this GameObject.
    /// They are discovered automatically on scene start.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherManager : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("Weather state applied immediately when the scene starts. Can be left empty.")]
        [SerializeField] private WeatherStateData initialState;

        [Header("Transition")]
        [Tooltip("Duration in seconds used by SetWeather(state) when no duration is specified.")]
        [SerializeField, Min(0f)] private float defaultTransitionDuration = 2f;

        [Tooltip("Curve that controls transition easing. " +
                 "EaseInOut produces the most natural results.")]
        [SerializeField] private AnimationCurve transitionCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // --- Internal State ---
        private WeatherModule[] _modules;
        private WeatherStateData _currentState;
        private WeatherStateData _fromState;
        private WeatherStateData _runtimeSnapshot;
        private Coroutine _activeTransition;
        private float _transitionProgress;

        // --- Public API ---

        /// <summary>The target state of the last SetWeather call.</summary>
        public WeatherStateData CurrentState => _currentState;

        /// <summary>True while a transition is running.</summary>
        public bool IsTransitioning => _activeTransition != null;

        /// <summary>
        /// Current transition progress [0, 1], already evaluated through the AnimationCurve.
        /// Returns 0 when no transition is active.
        /// </summary>
        public float TransitionProgress => _transitionProgress;

        // --- Lifecycle ---

        private void Awake()
        {
            _modules = GetComponentsInChildren<WeatherModule>(includeInactive: true);

            if (_modules.Length == 0)
                Debug.LogWarning("[WeatherManager] No child modules found. " +
                    "Add LightModule, FogModule, or other modules as child GameObjects.", this);
        }

        private void Start()
        {
            if (initialState != null)
                SetWeather(initialState, 0f);
        }

        private void OnDestroy()
        {
            DestroySnapshot();
        }

        // --- Public Methods ---

        /// <summary>
        /// Transitions to the given state using the default transition duration.
        /// </summary>
        public void SetWeather(WeatherStateData state)
            => SetWeather(state, defaultTransitionDuration);

        /// <summary>
        /// Transitions to the given state with a specific duration in seconds.
        /// If duration is 0 or less, the change is immediate.
        /// If called during an active transition, the current transition is interrupted
        /// and the new one begins from the exact current visual state.
        /// </summary>
        public void SetWeather(WeatherStateData state, float duration)
        {
            if (state == null)
            {
                Debug.LogWarning("[WeatherManager] The provided state is null. " +
                    "Make sure to assign a valid WeatherStateData.", this);
                return;
            }

            // If a transition is active, capture the current visual state
            // as the starting point for the new transition.
            if (_activeTransition != null)
            {
                StopCoroutine(_activeTransition);
                _activeTransition = null;
                _fromState = CreateSnapshot(_fromState, _currentState, _transitionProgress);
            }
            else
            {
                _fromState = _currentState;
            }

            _currentState = state;
            _transitionProgress = 0f;

            if (duration <= 0f || _fromState == null)
            {
                ApplyToAll(state);
                return;
            }

            _activeTransition = StartCoroutine(TransitionRoutine(_fromState, state, duration));
        }

        // --- Internal Logic ---

        private IEnumerator TransitionRoutine(WeatherStateData from, WeatherStateData to, float duration)
        {
            Debug.Log($"[WeatherManager] Starting transition: " +
                      $"<b>{from.stateName}</b> → <b>{to.stateName}</b> ({duration:F1}s)");

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);
                _transitionProgress = transitionCurve.Evaluate(rawT);

                BlendAll(from, to, _transitionProgress);
                yield return null;
            }

            // Apply the clean final state to avoid floating-point drift
            _transitionProgress = 1f;
            ApplyToAll(to);
            _activeTransition = null;

            // The snapshot is no longer needed
            DestroySnapshot();

            Debug.Log($"[WeatherManager] Transition complete: <b>{to.stateName}</b>");
        }

        private void ApplyToAll(WeatherStateData state)
        {
            foreach (var module in _modules)
            {
                if (module == null) continue;
                module.Apply(state);
            }
        }

        private void BlendAll(WeatherStateData from, WeatherStateData to, float t)
        {
            foreach (var module in _modules)
            {
                if (module == null) continue;
                module.Blend(from, to, t);
            }
        }

        /// <summary>
        /// Creates a temporary ScriptableObject representing the interpolated visual state
        /// at a given transition progress. Used only when a transition is interrupted by another.
        /// </summary>
        private WeatherStateData CreateSnapshot(WeatherStateData from, WeatherStateData to, float t)
        {
            if (from == null) return to;

            DestroySnapshot();

            _runtimeSnapshot = ScriptableObject.CreateInstance<WeatherStateData>();
            _runtimeSnapshot.stateName       = "_Snapshot";
            _runtimeSnapshot.lightColor      = Color.Lerp(from.lightColor,      to.lightColor,      t);
            _runtimeSnapshot.lightIntensity  = Mathf.Lerp(from.lightIntensity,  to.lightIntensity,  t);
            _runtimeSnapshot.ambientColor    = Color.Lerp(from.ambientColor,    to.ambientColor,    t);
            _runtimeSnapshot.fogColor        = Color.Lerp(from.fogColor,        to.fogColor,        t);
            _runtimeSnapshot.fogDensity      = Mathf.Lerp(from.fogDensity,      to.fogDensity,      t);
            // Keep fog active if either state had it enabled, preventing a pop on interruption.
            _runtimeSnapshot.fogEnabled      = from.fogEnabled || to.fogEnabled;
            _runtimeSnapshot.rainIntensity   = Mathf.Lerp(from.rainIntensity,   to.rainIntensity,   t);
            _runtimeSnapshot.rainColor       = Color.Lerp(from.rainColor,       to.rainColor,       t);
            _runtimeSnapshot.rainSpeed       = Mathf.Lerp(from.rainSpeed,       to.rainSpeed,       t);
            _runtimeSnapshot.skyboxTint      = Color.Lerp(from.skyboxTint,      to.skyboxTint,      t);
            _runtimeSnapshot.skyboxExposure  = Mathf.Lerp(from.skyboxExposure,  to.skyboxExposure,  t);
            // Use the dominant clip based on transition progress.
            // Before 50% the source clip is louder; after that, the destination clip takes over.
            _runtimeSnapshot.ambientClip     = t < 0.5f ? from.ambientClip : to.ambientClip;
            _runtimeSnapshot.ambientVolume   = Mathf.Lerp(from.ambientVolume,   to.ambientVolume,   t);

            return _runtimeSnapshot;
        }

        private void DestroySnapshot()
        {
            if (_runtimeSnapshot != null)
            {
                Destroy(_runtimeSnapshot);
                _runtimeSnapshot = null;
            }
        }
    }
}
