using UnityEngine;

/// <summary>
/// Put this on a world item prefab. Walking into it adds the item to the player's hotbar.
/// Needs a second collider on the object with Is Trigger checked.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private InventoryItem item;
    [Tooltip("Seconds before this can be picked up, so a dropped item isn't grabbed instantly.")]
    [SerializeField] private float armDelay = 1f;

    private float _spawnTime;

    void Start() => _spawnTime = Time.time;

    void OnTriggerEnter(Collider other)
    {
        if (item == null) return;
        if (Time.time - _spawnTime < armDelay) return;
        if (!other.CompareTag("Player")) return;

        Hotbar hotbar = other.GetComponentInParent<Hotbar>();
        if (hotbar == null) hotbar = other.GetComponentInChildren<Hotbar>();
        if (hotbar == null) return;

        if (hotbar.AddItem(item))
            Destroy(gameObject);
    }
}