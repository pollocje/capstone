using UnityEngine;

public class Hotbar : MonoBehaviour
{
    [Header("Settings")]
    public int slotCount = 5;
    public float dropDistance = 2f;
    public Transform playerTransform;

    [Header("UI")]
    public HotbarUI hotbarUI;

    [Header("Items")]
    public InventoryItem[] items;

    private int selectedIndex = 0;
    private Binoculars _binoculars;

    void Start()
    {
        _binoculars = GetComponent<Binoculars>();

        if (items.Length > slotCount)
            System.Array.Resize(ref items, slotCount);

        hotbarUI?.Refresh(items, selectedIndex);
    }

    void Update()
    {
        HandleNumberKeys();
        HandleUse();
    }

    // ── Input ────────────────────────────────────────────────────────────────

    void HandleNumberKeys()
    {
        for (int i = 0; i < slotCount && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                // If switching away from binoculars, reset the view
                if (GetSelectedItem()?.itemType == ItemType.Binoculars)
                    _binoculars?.ResetView();

                selectedIndex = i;
                hotbarUI?.UpdateSelection(selectedIndex);
            }
        }
    }

    void HandleUse()
    {
        InventoryItem item = GetSelectedItem();
        if (item == null) return;

        switch (item.itemType)
        {
            case ItemType.Droppable:
                if (Input.GetMouseButtonDown(0))
                    DropItem();
                break;

            case ItemType.Binoculars:
                // Hold left click to zoom, release to unzoom
                if (Input.GetMouseButtonDown(0))
                    _binoculars?.ToggleZoom();
                if (Input.GetMouseButtonUp(0))
                    _binoculars?.ResetView();
                break;
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    void DropItem()
    {
        InventoryItem item = GetSelectedItem();
        if (item == null || item.dropPrefab == null) return;

        Vector3 spawnPos = playerTransform.position
                         + playerTransform.forward * dropDistance
                         + Vector3.up * 0.5f;

        Instantiate(item.dropPrefab, spawnPos, playerTransform.rotation);

        items[selectedIndex] = null;
        hotbarUI?.Refresh(items, selectedIndex);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public InventoryItem GetSelectedItem()
    {
        if (selectedIndex < 0 || selectedIndex >= items.Length)
            return null;
        return items[selectedIndex];
    }

    public bool AddItem(InventoryItem item)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                items[i] = item;
                hotbarUI?.Refresh(items, selectedIndex);
                return true;
            }
        }
        return false;
    }
}