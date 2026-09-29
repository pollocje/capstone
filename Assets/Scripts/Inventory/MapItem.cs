using UnityEngine;

public class MapItem : MonoBehaviour, IEquippable
{
    [Header("Dependencies")]
    [SerializeField] private GameObject mapUI;      // MapOverlay on the Canvas
    [SerializeField] private GameObject mapVisual;  // map model held in front of the camera

    [Header("Settings")]
    [Tooltip("On: hold left click to view. Off: click to open, click again to close.")]
    [SerializeField] private bool holdToView = true;

    private bool _isUsing = false;

    public bool IsUsing => _isUsing;

    void Start()
    {
        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        _isUsing = open;
        if (mapUI != null) mapUI.SetActive(open);
        if (mapVisual != null) mapVisual.SetActive(open);
    }

    // ── IEquippable ──────────────────────────────────────────────────────────

    public void OnEquip() { }
    public void OnUnequip() => SetOpen(false);
    public void OnUseDown() => SetOpen(holdToView ? true : !_isUsing);
    public void OnUseUp() { if (holdToView) SetOpen(false); }
}