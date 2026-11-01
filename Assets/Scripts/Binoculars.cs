using UnityEngine;
using Cinemachine;

public class Binoculars : MonoBehaviour, IEquippable
{
    [Header("Dependencies")]
    public CinemachineVirtualCamera vcam;
    public GameObject binocularUI;
    [Tooltip("Model shown in hand. Only its meshes are copied, so the FBX or the world pickup prefab can be used directly.")]
    [SerializeField] private GameObject heldModel;
    [Tooltip("Camera the held binoculars are parented to. Found under the player root if left empty.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Settings")]
    public float zoomFov = 15f;
    public float normalFov = 60f;
    public float zoomSpeed = 5f;

    [Header("Hold pose (camera space)")]
    [SerializeField] private Vector3 holdPosition = new Vector3(0.2f, -0.22f, 0.5f);
    [SerializeField] private Vector3 holdEuler = Vector3.zero;
    [Tooltip("Multiplier on the held model's own scale.")]
    [SerializeField] private float holdScale = 1.5f;

    private bool _isUsing = false;
    private bool _equipped = false;
    private float _currentFov;
    private GameObject _heldVisual;

    public bool IsUsing => _isUsing;

    //AUDIO VARIABLES
    [SerializeField] AudioSource binocularSource;
    [SerializeField] AudioClip binocularSound;

    void Awake()
    {
        if (cameraTransform == null)
        {
            // MainCamera is a sibling of PlayerCapsule on the player prefab, so search from the root.
            var cam = transform.root.GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }

        // Built in Awake so it exists before Hotbar.Start can equip the starting slot.
        BuildHeldVisual();
    }

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

        if (!_isUsing && binocularSource != null && binocularSound != null)
            binocularSource.PlayOneShot(binocularSound);

        _isUsing = true;
        if (binocularUI != null) binocularUI.SetActive(true);
        UpdateHeldVisual();
    }

    /// <summary>Called by Hotbar when left click is released, or slot is switched.</summary>
    public void ResetView()
    {
        _isUsing = false;
        if (binocularUI != null) binocularUI.SetActive(false);
        UpdateHeldVisual();
    }

    void Update()
    {

        float target = _isUsing ? zoomFov : normalFov;

        if (vcam != null)
            vcam.m_Lens.FieldOfView = Mathf.Lerp(vcam.m_Lens.FieldOfView, target, Time.deltaTime * zoomSpeed);
    }

    // ── Held model ───────────────────────────────────────────────────────────

    void BuildHeldVisual()
    {
        if (heldModel == null || cameraTransform == null)
        {
            Debug.LogWarning($"Binoculars: {(heldModel == null ? "no heldModel assigned" : "no camera found")}, so nothing is shown in hand.", this);
            return;
        }

        _heldVisual = HeldModelUtility.CopyMeshes(heldModel.transform, cameraTransform);
        _heldVisual.name = "HeldBinoculars";

        Transform t = _heldVisual.transform;
        t.localPosition = holdPosition;
        t.localRotation = Quaternion.Euler(holdEuler);
        t.localScale = heldModel.transform.localScale * holdScale;

        _heldVisual.SetActive(false);
    }

    // Shown while the slot is selected, hidden while zoomed so it doesn't block the overlay.
    void UpdateHeldVisual()
    {
        if (_heldVisual != null) _heldVisual.SetActive(_equipped && !_isUsing);
    }

    // ── IEquippable ──────────────────────────────────────────────────────────

    public void OnEquip()
    {
        _equipped = true;
        UpdateHeldVisual();
    }

    public void OnUnequip()
    {
        _equipped = false;
        ResetView();
    }

    public void OnUseDown() => ToggleZoom();
    public void OnUseUp() => ResetView();
}
