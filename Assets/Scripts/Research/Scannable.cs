using UnityEngine;
using UnityEngine.Events;

/// <summary>Marks an object (e.g. a cloud) as scannable through the binoculars.</summary>
public class Scannable : MonoBehaviour
{
    [Tooltip("Research % added to the total bar when this finishes scanning.")]
    public float researchValue = 10f;

    [Tooltip("Seconds of continuous focus needed to complete the scan.")]
    public float scanDuration = 3f;

    public UnityEvent onScanned;

    public bool IsScanned { get; private set; }

    public void MarkScanned()
    {
        IsScanned = true;
        onScanned?.Invoke();
    }
}
