using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to the root of your Slot prefab.
/// 
/// Prefab hierarchy:
///   SlotRoot  (Image + SlotUI)
///     ├── ItemIcon      (Image)
///     └── SlotNumber    (TextMeshProUGUI)
/// 
/// Background is grabbed automatically from this GameObject's Image component.
/// Only ItemIcon and SlotNumber need to be assigned in the Inspector.
/// </summary>
public class SlotUI : MonoBehaviour
{
    [Header("Child References — assign in prefab")]
    public Image itemIcon;
    public TextMeshProUGUI slotNumber;

    // Grabbed automatically — no need to assign in Inspector
    private Image background;

    void Awake()
    {
        background = GetComponent<Image>();
    }

    public void SetNumber(int n)
    {
        if (slotNumber != null)
            slotNumber.text = n.ToString();
    }

    public void SetItem(InventoryItem item)
    {
        if (itemIcon == null) return;

        if (item != null && item.icon != null)
        {
            itemIcon.sprite  = item.icon;
            itemIcon.enabled = true;
        }
        else
        {
            itemIcon.sprite  = null;
            itemIcon.enabled = false;
        }
    }

    public void SetSelected(bool selected, Color normal, Color highlight)
    {
        if (background != null)
            background.color = selected ? highlight : normal;
    }
}