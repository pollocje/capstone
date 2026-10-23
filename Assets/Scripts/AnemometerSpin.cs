using UnityEngine;

public class AnemometerSpin : MonoBehaviour
{
    [Tooltip("Degrees per second the rotor spins when no WindZone is affecting it.")]
    public float ambientSpinSpeed = 10f;

    [Tooltip("Degrees per second added per unit of WindZone speed (windMain).")]
    public float spinSpeedMultiplier = 40f;

    [Tooltip("How quickly the spin speed catches up to its target; higher = snappier.")]
    public float smoothing = 3f;

    float currentSpinSpeed;

    void Update()
    {
        float targetSpinSpeed = ambientSpinSpeed;

        if (WindSampler.TryGetWindAt(transform.position, out float zoneSpeed, out _))
        {
            targetSpinSpeed = Mathf.Max(ambientSpinSpeed, zoneSpeed * spinSpeedMultiplier);
        }

        currentSpinSpeed = Mathf.Lerp(currentSpinSpeed, targetSpinSpeed, smoothing * Time.deltaTime);

        transform.Rotate(Vector3.up, currentSpinSpeed * Time.deltaTime, Space.Self);
    }
}
