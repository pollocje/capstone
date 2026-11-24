using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Marks an object (e.g. a cloud) as scannable through the binoculars. IsScanned is
/// shared across every player: once anyone finishes scanning this object, the host confirms it
/// and every client - including ones who never looked at it - gets the same IsScanned/onScanned.</summary>
public class Scannable : MonoBehaviour
{
    [Tooltip("Research % added to the total bar when this finishes scanning.")]
    public float researchValue = 10f;

    [Tooltip("Seconds of continuous focus needed to complete the scan.")]
    public float scanDuration = 3f;

    public UnityEvent onScanned;

    public bool IsScanned { get; private set; }

    int _index;

    void Awake()
    {
        _index = SceneIndex.Of(this);
    }

    void Start()
    {
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

        // Catch up immediately in case the host's "already scanned" credit (for a late join)
        // arrived before this object finished waking up and missed the event above.
        if (rm.IsConsumed(ResearchManager.Source.Scan, _index))
            OnSourceConsumed(ResearchManager.Source.Scan, _index);
    }

    void OnDestroy()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.onSourceConsumed -= OnSourceConsumed;
    }

    /// <summary>Called by BinocularScanner once its local fill bar completes. Reports to
    /// ResearchManager instead of flipping IsScanned directly - the actual state change only
    /// happens once the host confirms it via OnSourceConsumed (immediate if we are the host).</summary>
    public void ReportScanned()
    {
        if (IsScanned) return;
        ResearchManager.Instance?.AddResearch(researchValue, ResearchManager.Source.Scan, _index);
    }

    void OnSourceConsumed(ResearchManager.Source source, int index)
    {
        if (source != ResearchManager.Source.Scan || index != _index || IsScanned) return;
        IsScanned = true;
        onScanned?.Invoke();
    }
}
