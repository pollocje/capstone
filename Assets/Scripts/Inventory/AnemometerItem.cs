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
    [Tooltip("Optional floating HUD readout. Leave unassigned if you only want the on-model label below.")]
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

    [Header("On-model readout (curves around the held model's pole)")]
    [Tooltip("Font for the curved label. Leave empty to use TextMeshPro's project default.")]
    [SerializeField] private TMP_FontAsset modelLabelFont;
    [Tooltip("Local position of the label relative to the held model's root — slide this up/down " +
             "the pole and move it to sit right at the surface.")]
    [SerializeField] private Vector3 modelLabelLocalPosition = new Vector3(0f, 0.3f, 0f);
    [Tooltip("Local rotation (euler) of the label relative to the held model's root.")]
    [SerializeField] private Vector3 modelLabelLocalEuler = Vector3.zero;
    [Tooltip("RectTransform width/height — affects wrapping and the pivot the curve/rotation is " +
             "applied around, not just visual size.")]
    [SerializeField] private Vector2 modelLabelSize = new Vector2(16.5f, 6.7f);
    [SerializeField] private float modelLabelFontSize = 0.15f;
    [Tooltip("Curve radius — match it to the pole's visual radius at the label's height so the text hugs it.")]
    [SerializeField] private float modelLabelRadius = 0.3f;

    private bool _equipped;
    private GameObject _heldVisual;
    private TMP_Text _modelLabel;

    public bool IsEquipped => _equipped;

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

        WindSampler.TryGetWindAt(transform.position, out float speed, out _);
        string reading = $"Wind Speed: {speed:F1} m/s";

        if (windSpeedText != null) windSpeedText.text = reading;
        if (_modelLabel != null) _modelLabel.text = reading;
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

        BuildModelLabel(t);

        _heldVisual.SetActive(false);
    }

    // Curved 3D label mounted on the held root (not a child of Cylinder/Rotor — those carry a
    // baked FBX axis-correction rotation, same one that caused the rotor spin bug, so their own
    // local "up" isn't actually vertical. The root's frame is clean.
    void BuildModelLabel(Transform heldRoot)
    {
        var go = new GameObject("WindSpeedLabel");
        go.transform.SetParent(heldRoot, false);
        go.transform.localPosition = modelLabelLocalPosition;
        go.transform.localRotation = Quaternion.Euler(modelLabelLocalEuler);

        _modelLabel = go.AddComponent<TextMeshPro>();

        var rect = _modelLabel.rectTransform;
        rect.sizeDelta = modelLabelSize;
        _modelLabel.text = "Wind Speed: -- m/s";
        _modelLabel.fontSize = modelLabelFontSize;
        _modelLabel.alignment = TextAlignmentOptions.Center;
        _modelLabel.color = Color.white;
        if (modelLabelFont != null) _modelLabel.font = modelLabelFont;

        var curve = go.AddComponent<CurvedTMPText>();
        curve.radius = modelLabelRadius;
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
