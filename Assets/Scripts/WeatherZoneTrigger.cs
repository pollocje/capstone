using UnityEngine;
using DynamicWeatherSystem;

[RequireComponent(typeof(Collider))]
public class WeatherZoneTrigger : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private WeatherStateData targetState;
    [SerializeField] private float transitionDuration = 4f;

    [Header("Behaviour")]
    [Tooltip("If true, this trigger only fires once and then disables itself.")]
    [SerializeField] private bool triggerOnce = true;

    private bool _hasFired;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
        weatherManager = FindFirstObjectByType<WeatherManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFired && triggerOnce) return;
        if (other.GetComponentInParent<CharacterController>() == null) return;

        if (weatherManager == null || targetState == null)
        {
            Debug.LogWarning("[WeatherZoneTrigger] Missing WeatherManager or targetState reference.", this);
            return;
        }

        weatherManager.SetWeather(targetState, transitionDuration);
        _hasFired = true;

        if (triggerOnce)
            enabled = false;
    }
}
