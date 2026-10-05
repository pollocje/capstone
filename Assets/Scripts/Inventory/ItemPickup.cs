using UnityEngine;

/// <summary>
/// Put this on a world item prefab. Pressing Interact (E) while looking at or standing near it
/// adds the item to the player's hotbar (PlayerGrabController calls TryPickup).
/// With pickupOnTouch on, walking into it does the same — that needs a second collider on the
/// object with Is Trigger checked.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private InventoryItem item;
    [Tooltip("Seconds before this can be picked up, so a dropped item isn't grabbed instantly.")]
    [SerializeField] private float armDelay = 1f;
    [Tooltip("On: walking into the trigger picks it up. Off: only Interact (E) picks it up.")]
    [SerializeField] private bool pickupOnTouch = true;

    private float _spawnTime;

    public InventoryItem Item => item;
    public bool IsArmed => Time.time - _spawnTime >= armDelay;

    void Start() => _spawnTime = Time.time;

    void OnTriggerEnter(Collider other)
    {
        if (!pickupOnTouch) return;
        if (!other.CompareTag("Player")) return;

        Hotbar hotbar = other.GetComponentInParent<Hotbar>();
        if (hotbar == null) hotbar = other.GetComponentInChildren<Hotbar>();
        TryPickup(hotbar);
    }

    /// <summary>Puts the item in the first empty hotbar slot and removes it from the world.</summary>
    public bool TryPickup(Hotbar hotbar)
    {
        if (item == null || hotbar == null) return false;
        if (!IsArmed) return false;

        if (!hotbar.AddItem(item)) return false;

        Destroy(gameObject);
        return true;
    }
}
