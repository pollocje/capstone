using UnityEngine;

public class TruckFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Distance / Height")]
    [SerializeField] private float distance = 8f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float positionSmoothSpeed = 8f;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("Mouse Orbit")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = 10f;
    [SerializeField] private float maxPitch = 45f;

    private float yaw;
    private float pitch = 15f;

    private void OnEnable()
    {
        if (target != null)
            yaw = target.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * mouseSensitivity;
        pitch -= mouseY * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * distance) + Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

        Quaternion lookRotation = Quaternion.LookRotation((target.position + Vector3.up * 1.2f) - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}