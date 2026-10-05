using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Hotbar map. While its slot is selected, a copy of the map model sits fixed in front of the
/// camera (plain parented transform — no Rigidbody or joint, so it can't swing or spin), and
/// Tab toggles the full-screen MapOverlay showing the overhead MapCamera's render texture.
/// Unequipping (switching slot or dropping) closes the view and hides the model.
/// </summary>
public class MapItem : MonoBehaviour, IEquippable
{
    [Header("Dependencies")]
    [SerializeField] private GameObject mapUI;      // MapOverlay on the HUD Canvas
    [Tooltip("Model shown in hand. Only its meshes are copied, so the world prefab (Item_Map) can be used directly.")]
    [SerializeField] private GameObject heldModel;
    [Tooltip("Camera the held map is parented to. Found under the player root if left empty.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("Hold pose (camera space)")]
    [SerializeField] private Vector3 holdPosition = new Vector3(0.22f, -0.22f, 0.5f);
    [SerializeField] private Vector3 holdEuler = new Vector3(-65f, 0f, 0f);
    [Tooltip("Multiplier on the held model's own scale.")]
    [SerializeField] private float holdScale = 0.5f;

    private bool _equipped;
    private bool _isUsing;
    private GameObject _heldVisual;
    private PlayerInput _playerInput;

    public bool IsEquipped => _equipped;
    public bool IsUsing => _isUsing;
    public GameObject HeldVisual => _heldVisual;

    void Awake()
    {
        if (cameraTransform == null)
        {
            // MainCamera is a sibling of PlayerCapsule on the player prefab, so search from the root.
            var cam = transform.root.GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }
        _playerInput = GetComponentInParent<PlayerInput>();
        if (_playerInput == null) _playerInput = transform.root.GetComponentInChildren<PlayerInput>(true);

        // Built in Awake so it exists before Hotbar.Start can equip the starting slot.
        BuildHeldVisual();
        SetOpen(false);
    }

    void Update()
    {
        if (!_equipped) return;
        // Remote players have PlayerInput disabled (PlayerNetworkSetup) — don't react to local keys there.
        if (_playerInput != null && !_playerInput.enabled) return;

        if (Input.GetKeyDown(toggleKey)) ToggleView();
    }

    public void ToggleView() => SetOpen(!_isUsing);

    private void SetOpen(bool open)
    {
        _isUsing = open;
        if (mapUI != null) mapUI.SetActive(open);
    }

    // ── Held model ───────────────────────────────────────────────────────────

    void BuildHeldVisual()
    {
        if (heldModel == null || cameraTransform == null)
        {
            Debug.LogWarning($"MapItem: {(heldModel == null ? "no heldModel assigned" : "no camera found")}, so nothing is shown in hand.", this);
            return;
        }

        _heldVisual = CopyMeshes(heldModel.transform, cameraTransform);
        _heldVisual.name = "HeldMap";

        Transform t = _heldVisual.transform;
        t.localPosition = holdPosition;
        t.localRotation = Quaternion.Euler(holdEuler);
        t.localScale = heldModel.transform.localScale * holdScale;

        _heldVisual.SetActive(false);
    }

    // Copies only MeshFilter/MeshRenderer, so the copy has no colliders, Rigidbody or pickup scripts.
    static GameObject CopyMeshes(Transform source, Transform parent)
    {
        var go = new GameObject(source.name);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = source.localScale;

        var filter = source.GetComponent<MeshFilter>();
        var renderer = source.GetComponent<MeshRenderer>();
        if (filter != null && renderer != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var copy = go.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (Transform child in source)
            CopyMeshes(child, go.transform);

        return go;
    }

    // ── IEquippable ──────────────────────────────────────────────────────────

    public void OnEquip()
    {
        _equipped = true;
        if (_heldVisual != null) _heldVisual.SetActive(true);
    }

    public void OnUnequip()
    {
        _equipped = false;
        SetOpen(false);
        if (_heldVisual != null) _heldVisual.SetActive(false);
    }

    // Left click does nothing for the map — Tab opens it (see Update).
    public void OnUseDown() { }
    public void OnUseUp() { }
}
