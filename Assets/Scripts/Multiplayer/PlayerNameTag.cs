using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shows each player's nickname above their head in-game.
/// The owning player sends their saved nickname once when they spawn; the
/// NetworkVariable then keeps it in sync for everyone (including late joiners).
///
/// Put this on PlayerCapsule and assign a 3D TextMeshPro object (the tag).
/// The tag is detached from the player at spawn and follows the head, so the
/// player's own scale can't stretch or squash it.
/// </summary>
public class PlayerNameTag : NetworkBehaviour
{
    // The 3D text above the player's head (GameObject -> 3D Object -> Text - TextMeshPro).
    [SerializeField] TextMeshPro nameText;
    // Hide your own name tag (you can't see your own head in first person anyway).
    [SerializeField] bool hideOwnTag = true;

    // How far above PlayerCapsule's position (its feet) the tag floats, in metres.
    [SerializeField] float heightOffset = 2.2f;
    // Text size. Around 2-4 is readable at a normal distance.
    [SerializeField] float fontSize = 3f;

    // Synced name. Only the owner can write it; everyone can read it.
    readonly NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    /// <summary>This player's display name (other scripts can use this).</summary>
    public string DisplayName => playerName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        // The owner tells everyone their nickname.
        if (IsOwner)
            playerName.Value = new FixedString64Bytes(PlayerNameStore.Get());

        if (nameText != null)
        {
            // Detach so the player's scale/rotation can't distort the tag, then reset its size.
            nameText.transform.SetParent(null, false);
            nameText.transform.localScale = Vector3.one;

            nameText.enableAutoSizing = false;
            nameText.fontSize = fontSize;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;

            nameText.gameObject.SetActive(!(IsOwner && hideOwnTag));
        }

        UpdateTag();
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;

        // The tag is no longer a child of the player, so clean it up ourselves.
        if (nameText != null)
            Destroy(nameText.gameObject);
    }

    void OnNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        UpdateTag();
    }

    void UpdateTag()
    {
        if (nameText != null)
            nameText.text = playerName.Value.ToString();
    }

    void LateUpdate()
    {
        if (nameText == null || !nameText.gameObject.activeSelf) return;

        // Follow the player's head.
        nameText.transform.position = transform.position + Vector3.up * heightOffset;

        // Turn the tag to face the LOCAL player's camera so it's always readable.
        Camera cam = GetLocalCamera();
        if (cam == null) return;

        // Only turn left/right (around the vertical axis) so the tag always stands upright,
        // even if the camera is tilted up or down.
        Vector3 away = nameText.transform.position - cam.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f)
            nameText.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    // The camera of the player on THIS machine (not Camera.main, which could be another
    // camera in the scene tagged MainCamera, e.g. the truck camera).
    static Camera _localCam;
    static Camera GetLocalCamera()
    {
        if (_localCam != null && _localCam.isActiveAndEnabled) return _localCam;

        var nm = NetworkManager.Singleton;
        var localPlayer = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        if (localPlayer != null)
        {
            // Skip cameras that render to a texture (minimaps, etc.) — we want the one drawing the screen.
            foreach (var c in localPlayer.GetComponentsInChildren<Camera>())
                if (c.isActiveAndEnabled && c.targetTexture == null) { _localCam = c; return c; }
        }

        // While driving, the player's own camera is off and the truck camera is used instead.
        _localCam = Camera.main;
        return _localCam;
    }
}