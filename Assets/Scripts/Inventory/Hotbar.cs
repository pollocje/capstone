using System.Collections.Generic;
using UnityEngine;

public class Hotbar : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode dropKey = KeyCode.G;
    public int slotCount = 5;
    public float dropDistance = 2f;
    public Transform playerTransform;

    [Header("UI")]
    public HotbarUI hotbarUI;

    [Header("Items")]
    public InventoryItem[] items;

    private int selectedIndex = 0;
    private readonly Dictionary<ItemType, IEquippable> _equippables = new Dictionary<ItemType, IEquippable>();
    private PlayerGrabController _grab;

    void Start()
    {
        RegisterEquippable(ItemType.Binoculars, GetComponent<Binoculars>());
        RegisterEquippable(ItemType.Firework, GetComponent<FireworkFlareLauncher>());
        RegisterEquippable(ItemType.Map, GetComponent<MapItem>());
        _grab = GetComponent<PlayerGrabController>();

        // If not manually assigned, find the spawned player by tag
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // One entry per slot, so slots beyond the starting items exist as empty slots
        // that pickups can fill (the prefab only lists the starting items).
        if (items == null || items.Length != slotCount)
            System.Array.Resize(ref items, slotCount);

        hotbarUI?.Refresh(items, selectedIndex);
        GetEquippable(GetSelectedItem())?.OnEquip();
    }

    void Update()
    {
        HandleNumberKeys();
        HandleUse();
        HandleDrop();
    }

    // ── Input ────────────────────────────────────────────────────────────────

    void HandleNumberKeys()
    {
        for (int i = 0; i < slotCount && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                SelectSlot(i);
        }
    }

    void HandleUse()
    {
        // While physically carrying something, left click belongs to the carried item.
        if (_grab != null && _grab.Held != null) return;

        InventoryItem item = GetSelectedItem();
        if (item == null) return;

        if (item.itemType == ItemType.Droppable)
        {
            if (Input.GetMouseButtonDown(0))
                DropItem();
            return;
        }

        IEquippable equippable = GetEquippable(item);
        if (equippable == null) return;

        if (Input.GetMouseButtonDown(0)) equippable.OnUseDown();
        if (Input.GetMouseButtonUp(0)) equippable.OnUseUp();
    }
void HandleDrop()
{
    if (!Input.GetKeyDown(dropKey)) return;

    InventoryItem item = GetSelectedItem();
    if (item == null || item.dropPrefab == null) return;
    if (item.itemType == ItemType.Droppable) return;  // those drop on left click already

    GetEquippable(item)?.OnUnequip();
    DropItem();
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

        ConsumeSelected();
    }

    /// <summary>Clears the currently selected slot (used by one-shot/consumable items).</summary>
    public void ConsumeSelected()
    {
        items[selectedIndex] = null;
        hotbarUI?.Refresh(items, selectedIndex);
    }

    /// <summary>Selects a slot (what the number keys do): unequips the old item, equips the new one.</summary>
    public void SelectSlot(int index)
    {
        if (index < 0 || index >= items.Length || index == selectedIndex) return;

        GetEquippable(GetSelectedItem())?.OnUnequip();

        selectedIndex = index;
        hotbarUI?.UpdateSelection(selectedIndex);

        GetEquippable(GetSelectedItem())?.OnEquip();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void RegisterEquippable(ItemType type, IEquippable equippable)
    {
        if (equippable != null)
            _equippables[type] = equippable;
    }

    IEquippable GetEquippable(InventoryItem item)
    {
        if (item == null) return null;
        _equippables.TryGetValue(item.itemType, out var equippable);
        return equippable;
    }

    public InventoryItem GetSelectedItem()
    {
        if (selectedIndex < 0 || selectedIndex >= items.Length)
            return null;
        return items[selectedIndex];
    }

    /// <summary>Puts the item in the lowest-numbered empty slot. Returns false if the hotbar is full.</summary>
    public bool AddItem(InventoryItem item)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                items[i] = item;
                hotbarUI?.Refresh(items, selectedIndex);

                // Landed in the slot already in hand — equip it now, same as pressing its number key.
                if (i == selectedIndex) GetEquippable(item)?.OnEquip();
                return true;
            }
        }
        return false;
    }

    /// <summary>Slot index holding this item, or -1.</summary>
    public int IndexOf(InventoryItem item) => item == null ? -1 : System.Array.IndexOf(items, item);
}