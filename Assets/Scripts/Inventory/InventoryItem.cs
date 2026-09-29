using UnityEngine;

public enum ItemType
{
    Droppable,      // e.g. Barrel — left click drops it
    Binoculars,     // left click hold to zoom
    Firework,       // left click throws a flare; consumed on use
    Map             // left click hold to view the map
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class InventoryItem : ScriptableObject
{
    public string itemName = "Item";
    public Sprite icon;
    public ItemType itemType = ItemType.Droppable;
    public GameObject dropPrefab;   // Only needed for Droppable items
}