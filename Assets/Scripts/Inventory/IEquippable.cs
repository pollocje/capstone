/// <summary>
/// Implemented by player-side components (siblings of Hotbar) that back a non-Droppable
/// ItemType. Hotbar looks these up per ItemType and dispatches equip lifecycle + use input
/// to them instead of hardcoding per-item behavior.
/// </summary>
public interface IEquippable
{
    /// <summary>Called when this item's slot becomes the selected hotbar slot.</summary>
    void OnEquip();

    /// <summary>Called when switching away from this item's slot.</summary>
    void OnUnequip();

    /// <summary>Called on the frame left click is pressed while this item is selected.</summary>
    void OnUseDown();

    /// <summary>Called on the frame left click is released while this item is selected.</summary>
    void OnUseUp();
}
