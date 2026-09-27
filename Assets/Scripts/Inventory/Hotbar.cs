using System.Collections.Generic;
using UnityEngine;

public class Hotbar : MonoBehaviour
{
    [Header("Settings")]
    public int slotCount = 5;
    public float dropDistance = 2f;
    public Transform playerTransform;

    [Header("UI")]
    public HotbarUI hotbarUI;

    // Always starts empty (see Start) — items are picked up in-game, e.g. from an ItemDispenser
    [HideInInspector] public InventoryItem[] items;

    private int selectedIndex = 0;
    private readonly Dictionary<ItemType, IEquippable> _equippables = new Dictionary<ItemType, IEquippable>();

    void Start()
    {
        RegisterEquippable(ItemType.Binoculars, GetComponent<Binoculars>());
        RegisterEquippable(ItemType.Firework, GetComponent<FireworkFlareLauncher>());

        // If not manually assigned, find the spawned player by tag
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // Every player spawns with an empty hotbar, one slot per slotCount
        items = new InventoryItem[slotCount];

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
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i != selectedIndex)
            {
                GetEquippable(GetSelectedItem())?.OnUnequip();

                selectedIndex = i;
                hotbarUI?.UpdateSelection(selectedIndex);

                GetEquippable(GetSelectedItem())?.OnEquip();
            }
        }
    }

    void HandleUse()
    {
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

    public bool HasFreeSlot()
    {
        return System.Array.IndexOf(items, null) >= 0;
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
