using UnityEngine;
using Cinemachine;

public class Binoculars : MonoBehaviour
{
    [Header("Dependencies")]
    public CinemachineVirtualCamera vcam;
    public GameObject binocularUI;

    [Header("Settings")]
    public float zoomFov = 15f;
    public float normalFov = 60f;
    public float zoomSpeed = 5f;

    private bool _isUsing = false;
    private float _currentFov;

    void Start()
    {
        if (vcam == null)
            vcam = Object.FindFirstObjectByType<CinemachineVirtualCamera>();

        if (binocularUI == null)
        {
            binocularUI = transform.GetComponentInChildren<Canvas>(true)?
                        .transform.Find("BinocularOverlay")?.gameObject;
        }

        _currentFov = normalFov;
    }

    /// <summary>Called by Hotbar when left click is pressed on binoculars slot.</summary>
    public void ToggleZoom()
    {
        _isUsing = true;
        if (binocularUI != null) binocularUI.SetActive(true);
    }

    /// <summary>Called by Hotbar when left click is released, or slot is switched.</summary>
    public void ResetView()
    {
        _isUsing = false;
        if (binocularUI != null) binocularUI.SetActive(false);
    }

    void Update()
    {
        float target = _isUsing ? zoomFov : normalFov;

        if (vcam != null)
            vcam.m_Lens.FieldOfView = Mathf.Lerp(vcam.m_Lens.FieldOfView, target, Time.deltaTime * zoomSpeed);
    }
}