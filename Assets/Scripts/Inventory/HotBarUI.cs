using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas-based hotbar UI.
/// 
/// SETUP:
/// 1. Create a Canvas (Screen Space - Overlay, scale with screen size, 1920x1080 reference)
/// 2. Create an empty child GameObject on the Canvas called "HotbarUI"
/// 3. Attach this script to that GameObject
/// 4. Assign the SlotPrefab in the Inspector (see below for prefab structure)
/// 5. Assign the HotbarRoot RectTransform (a horizontal layout group container)
/// 
/// SLOT PREFAB STRUCTURE:
///   SlotRoot (Image + SlotUI script) ← background
///     └── ItemIcon (Image)           ← item sprite, centered
///     └── SlotNumber (TextMeshProUGUI) ← top-left corner number label
/// </summary>
public class HotbarUI : MonoBehaviour
{
    [Header("References")]
    public RectTransform hotbarRoot;  // Container with Horizontal Layout Group
    public GameObject slotPrefab;     // Prefab for a single slot (see structure above)

    [Header("Appearance")]
    public Color normalColor   = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    public Color selectedColor = new Color(0.9f, 0.7f, 0.1f, 0.90f);

    private SlotUI[] slots;
    private int selectedIndex;

    // ── Public API ───────────────────────────────────────────────────────────

    public void Refresh(InventoryItem[] items, int newIndex)
    {
        selectedIndex = newIndex;

        // Build slot pool on first call or if count changed
        if (slots == null || slots.Length != items.Length)
            BuildSlots(items.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].SetItem(items[i]);
            slots[i].SetSelected(i == selectedIndex, normalColor, selectedColor);
        }
    }

    public void UpdateSelection(int newIndex)
    {
        if (slots == null) return;

        if (selectedIndex >= 0 && selectedIndex < slots.Length)
            slots[selectedIndex].SetSelected(false, normalColor, selectedColor);

        selectedIndex = newIndex;

        if (selectedIndex >= 0 && selectedIndex < slots.Length)
            slots[selectedIndex].SetSelected(true, normalColor, selectedColor);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    void BuildSlots(int count)
    {
        foreach (Transform child in hotbarRoot)
            Destroy(child.gameObject);

        slots = new SlotUI[count];

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(slotPrefab, hotbarRoot);
            slots[i] = go.GetComponent<SlotUI>();
            slots[i].SetNumber(i + 1);
        }
    }
}