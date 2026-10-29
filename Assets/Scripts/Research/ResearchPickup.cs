using System.Collections;
using UnityEngine;

/// <summary>Walk-over pickup (coin/note style) that adds to the shared research bar and removes
/// itself. Needs a trigger collider on this object (or a child) and the player to have the
/// "Player" tag. Collection is shared across every player: the first client to trigger it reports
/// to the host, and every client - including whoever didn't trigger it - removes the object once
/// the host confirms, so it can't be picked up twice.</summary>
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
    private int _index;
    private bool _collected;

    void Awake() => _index = SceneIndex.Of(this);

    void Start()
    {
        _startLocalPos = transform.localPosition;
        StartCoroutine(WaitAndSubscribe());
    }

    // ResearchManager lives on the player prefab, spawned at runtime by the networked lobby
    // flow, so it may not exist yet when this scene object wakes up.
    IEnumerator WaitAndSubscribe()
    {
        while (ResearchManager.Instance == null)
            yield return null;

        var rm = ResearchManager.Instance;
        rm.onSourceConsumed += OnSourceConsumed;

        // Catch up immediately in case the host's "already collected" credit (for a late join)
        // arrived before this object finished waking up and missed the event above.
        if (rm.IsConsumed(ResearchManager.Source.Pickup, _index))
            OnSourceConsumed(ResearchManager.Source.Pickup, _index);
    }

    void OnDestroy()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.onSourceConsumed -= OnSourceConsumed;
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
        transform.localPosition = _startLocalPos + Vector3.up * yOffset;

        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Player")) return;

        ResearchManager.Instance?.AddResearch(researchValue, ResearchManager.Source.Pickup, _index);
    }

    void OnSourceConsumed(ResearchManager.Source source, int index)
    {
        if (source != ResearchManager.Source.Pickup || index != _index || _collected) return;
        _collected = true;
        Destroy(gameObject);
    }
}
