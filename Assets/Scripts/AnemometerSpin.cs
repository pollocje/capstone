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

        // The rotor mesh's own "up" is skewed sideways by a baked FBX import rotation, and world-up
        // isn't right either once this is the held copy (which tilts with the camera). The parent
        // (the pole, in-world or the held-item root) is always upright in its own frame, so spin
        // around its current up direction instead of a fixed axis.
        Vector3 axis = transform.parent != null ? transform.parent.up : Vector3.up;
        transform.Rotate(axis, currentSpinSpeed * Time.deltaTime, Space.World);
    }
}
