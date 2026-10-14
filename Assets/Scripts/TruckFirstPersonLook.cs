using UnityEngine;

/// <summary>
/// Lets the driver look around the cab while in first-person view. Rotates purely in local
/// space relative to the parent (DriverSeat), which already tracks the truck's position and
/// facing - so this only ever has to handle head turning, not following the vehicle.
/// </summary>
public class TruckFirstPersonLook : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float maxYaw = 90f;
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 45f;

    private float yaw;
    private float pitch;

    private void OnEnable()
    {
        // Re-center looking forward every time this view becomes active.
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
