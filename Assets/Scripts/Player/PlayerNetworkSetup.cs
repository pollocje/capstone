using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using StarterAssets;

public class PlayerNetworkSetup : NetworkBehaviour
{
    private GameObject _cameraRoot;

    void Awake()
    {
        var cam = GetComponentInChildren<Camera>(includeInactive: true);
        _cameraRoot = cam != null ? cam.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        // Disable non-owned players immediately
        if (!IsOwner)
        {
            SetCameraEnabled(false);
            SetPlayerControlsEnabled(false);
            return;
        }

        // For the owning client, wait one frame before enabling the camera so any
        // other players' CinemachineBrains are already disabled — prevents Cinemachine
        // latching onto the wrong brain during the brief window when all are active.
        SetPlayerControlsEnabled(true);
        StartCoroutine(EnableCameraNextFrame());
    }

    System.Collections.IEnumerator EnableCameraNextFrame()
    {
        yield return null;
        SetCameraEnabled(true);
    }

    void SetCameraEnabled(bool enabled)
    {
        if (_cameraRoot != null)
            _cameraRoot.SetActive(enabled);
    }

    void SetPlayerControlsEnabled(bool enabled)
    {
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.enabled = enabled;

        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = enabled;

        var inputs = GetComponent<StarterAssetsInputs>();
        if (inputs != null) inputs.enabled = enabled;
    }
}
