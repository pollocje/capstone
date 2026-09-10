using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using StarterAssets;
using Cinemachine;

public class PlayerNetworkSetup : NetworkBehaviour
{
    private GameObject _cameraRoot;   // MainCamera GO (Camera + CinemachineBrain)
    private GameObject _vcamRoot;     // PlayerFollowCamera GO (VirtualCamera)

    void Awake()
    {
        var cam = GetComponentInChildren<Camera>(includeInactive: true);
        _cameraRoot = cam != null ? cam.gameObject : null;

        var vcam = GetComponentInChildren<CinemachineVirtualCamera>(includeInactive: true);
        _vcamRoot = vcam != null ? vcam.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        // Disable non-owned players immediately — camera and VCam both off so
        // their VCam doesn't compete with the owner's on the shared CinemachineBrain.
        if (!IsOwner)
        {
            SetCameraEnabled(false);
            SetPlayerControlsEnabled(false);
            return;
        }

        // For the owning client, wait one frame before enabling so all non-owner
        // cameras/VCams are already disabled before ours comes online.
        SetPlayerControlsEnabled(true);
        HookUpHotbar();
        StartCoroutine(EnableCameraNextFrame());
    }

    void HookUpHotbar()
    {
        // hotbarUI lives on a scene Canvas, so it can't be wired inside the player
        // prefab itself — connect it here now that this client's player is spawned.
        var hotbar = GetComponentInChildren<Hotbar>(true);
        if (hotbar == null || hotbar.hotbarUI != null) return;

        hotbar.hotbarUI = FindFirstObjectByType<HotbarUI>();
    }

    System.Collections.IEnumerator EnableCameraNextFrame()
    {
        yield return null;
        SetCameraEnabled(true);
    }

    void SetCameraEnabled(bool enabled)
    {
        if (_cameraRoot != null) _cameraRoot.SetActive(enabled);
        if (_vcamRoot != null)   _vcamRoot.SetActive(enabled);
    }

    void SetPlayerControlsEnabled(bool enabled)
    {
        // Components live on PlayerCapsule (child of NestedParent root), not the root itself.
        var playerInput = GetComponentInChildren<PlayerInput>();
        if (playerInput != null) playerInput.enabled = enabled;

        var fpc = GetComponentInChildren<FirstPersonController>();
        if (fpc != null) fpc.enabled = enabled;

        var inputs = GetComponentInChildren<StarterAssetsInputs>();
        if (inputs != null) inputs.enabled = enabled;
    }
}
