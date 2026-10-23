using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// Hotbar anemometer. While its slot is selected, a copy of the anemometer model (rotor included)
/// sits fixed in front of the camera, and a small HUD readout shows the current wind speed at the
/// player's position, refreshed live from WindSampler.
/// </summary>
public class AnemometerItem : MonoBehaviour, IEquippable
{
    [Header("Dependencies")]
    [SerializeField] private TextMeshProUGUI windSpeedText;
    [Tooltip("Model shown in hand. Only its meshes are copied, so the world prefab (Anemeter) can be used directly.")]
    [SerializeField] private GameObject heldModel;
    [Tooltip("Camera the held anemometer is parented to. Found under the player root if left empty.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Hold pose (camera space)")]
    [SerializeField] private Vector3 holdPosition = new Vector3(0.25f, -0.25f, 0.5f);
    [SerializeField] private Vector3 holdEuler = Vector3.zero;
    [Tooltip("Multiplier on the held model's own scale.")]
    [SerializeField] private float holdScale = 0.5f;

    private bool _equipped;
    private GameObject _heldVisual;

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
        if (windSpeedText != null) windSpeedText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_equipped) return;
        if (windSpeedText == null) return;

        WindSampler.TryGetWindAt(transform.position, out float speed, out _);
        windSpeedText.text = $"Wind Speed: {speed:F1} m/s";
    }

    // ── Held model ───────────────────────────────────────────────────────────

    void BuildHeldVisual()
    {
        if (heldModel == null || cameraTransform == null)
        {
            Debug.LogWarning($"AnemometerItem: {(heldModel == null ? "no heldModel assigned" : "no camera found")}, so nothing is shown in hand.", this);
            return;
        }

        _heldVisual = CopyMeshes(heldModel.transform, cameraTransform);
        _heldVisual.name = "HeldAnemometer";

        Transform t = _heldVisual.transform;
        t.localPosition = holdPosition;
        t.localRotation = Quaternion.Euler(holdEuler);
        t.localScale = heldModel.transform.localScale * holdScale;

        // CopyMeshes strips scripts along with colliders/rigidbodies, so the rotor's spin needs to
        // be re-added on the matching held child, copying the source's tuning.
        var sourceSpin = heldModel.GetComponentInChildren<AnemometerSpin>();
        if (sourceSpin != null)
        {
            Transform heldRotor = FindChildByName(t, sourceSpin.gameObject.name);
            if (heldRotor != null)
            {
                var spin = heldRotor.gameObject.AddComponent<AnemometerSpin>();
                spin.ambientSpinSpeed = sourceSpin.ambientSpinSpeed;
                spin.spinSpeedMultiplier = sourceSpin.spinSpeedMultiplier;
                spin.smoothing = sourceSpin.smoothing;
            }
        }

        _heldVisual.SetActive(false);
    }

    static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
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
        if (windSpeedText != null) windSpeedText.gameObject.SetActive(true);
    }

    public void OnUnequip()
    {
        _equipped = false;
        if (_heldVisual != null) _heldVisual.SetActive(false);
        if (windSpeedText != null) windSpeedText.gameObject.SetActive(false);
    }

    // Passive readout — left click does nothing.
    public void OnUseDown() { }
    public void OnUseUp() { }
}
