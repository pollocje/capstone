using UnityEngine;

/// <summary>
/// Lets the driver look around the cab while in first-person view. Only the mouse moves this
/// camera - the truck's own body roll/pitch (the LuigiGameDev car physics leans the chassis
/// under steering/suspension) is deliberately NOT inherited, otherwise every steering input
/// shakes the view. We still turn with the truck's actual heading (yaw), computed fresh each
/// frame from the parent seat's forward vector flattened onto the horizontal plane, rather than
/// just parenting under the seat's full rotation.
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
    }

    private void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Flatten the seat's forward onto the horizontal plane so only the truck's heading
        // (not its roll/pitch from steering physics) feeds into the base orientation.
        Vector3 levelForward = transform.parent.forward;
        levelForward.y = 0f;
        if (levelForward.sqrMagnitude < 0.0001f) levelForward = Vector3.ProjectOnPlane(transform.parent.up, Vector3.up);
        Quaternion levelBase = Quaternion.LookRotation(levelForward.normalized, Vector3.up);

        transform.rotation = levelBase * Quaternion.Euler(pitch, yaw, 0f);
    }
}
