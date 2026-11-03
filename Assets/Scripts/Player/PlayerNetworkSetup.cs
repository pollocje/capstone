using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using StarterAssets;
using Cinemachine;

public class PlayerNetworkSetup : NetworkBehaviour
{
    private GameObject _cameraRoot;   // MainCamera GO (Camera + CinemachineBrain)
    private GameObject _vcamRoot;     // PlayerFollowCamera GO (VirtualCamera)
    private GameObject _hudRoot;      // HUD Canvas GO (ResearchManager, ScanBar, BinocularOverlay)

    void Awake()
    {
        var cam = GetComponentInChildren<Camera>(includeInactive: true);
        _cameraRoot = cam != null ? cam.gameObject : null;

        var vcam = GetComponentInChildren<CinemachineVirtualCamera>(includeInactive: true);
        _vcamRoot = vcam != null ? vcam.gameObject : null;

        var hud = GetComponentInChildren<Canvas>(includeInactive: true);
        _hudRoot = hud != null ? hud.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        // Disable non-owned players immediately — camera and VCam both off so their VCam
        // doesn't compete with the owner's on the shared CinemachineBrain. The HUD Canvas goes
        // too: it's the same prefab on every spawned copy, so leaving it live on ghost copies
        // meant ResearchManager.Awake() ran once per ghost as well as once for the real owner,
        // and its duplicate-singleton check could destroy whichever ResearchManager lost that
        // race - including the owner's own, before its Start() ever initialized the visible bar.
        if (!IsOwner)
        {
            SetCameraEnabled(false);
            SetPlayerControlsEnabled(false);
            if (_hudRoot != null) _hudRoot.SetActive(false);
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
        hotbar.RefreshUI();
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

        var fpc = GetComponentInChildren<StarterAssets.FirstPersonController>();
        if (fpc != null) fpc.enabled = enabled;

        var inputs = GetComponentInChildren<StarterAssetsInputs>();
        if (inputs != null) inputs.enabled = enabled;

        var cc = GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = enabled;
    }
}
