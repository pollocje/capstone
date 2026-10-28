using UnityEngine;

/// <summary>Walk-over pickup (coin/note style) that adds to the research bar and removes itself.
/// Needs a trigger collider on this object (or a child) and the player to have the "Player" tag.</summary>
public class ResearchPickup : MonoBehaviour
{
    [Tooltip("Research % added to ResearchManager's bar when picked up.")]
    [SerializeField] private float researchValue = 5f;

    [Header("Bob")]
    [Tooltip("How far up/down the item bobs, in units.")]
    [SerializeField] private float bobHeight = 0.25f;
    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float bobSpeed = 1f;
    [Tooltip("Degrees per second spun around the up axis. 0 = no spin.")]
    [SerializeField] private float spinSpeed = 90f;

    private Vector3 _startLocalPos;

    void Start() => _startLocalPos = transform.localPosition;

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
        transform.localPosition = _startLocalPos + Vector3.up * yOffset;

        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ResearchManager.Instance?.AddResearch(researchValue);
        Destroy(gameObject);
    }
}
