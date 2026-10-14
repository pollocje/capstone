using Microlight.MicroBar;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Persistent HUD bar tracking total cloud research progress.</summary>
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    [SerializeField] private MicroBar researchBar;
    [SerializeField] private float maxResearch = 100f;

    [Tooltip("Fires once, the moment the research bar reaches its max value.")]
    public UnityEvent onResearchComplete;

    private bool _completed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (researchBar == null)
        {
            Debug.LogError("ResearchManager: researchBar is not assigned.", this);
            return;
        }

        researchBar.Initialize(maxResearch);
        researchBar.UpdateBar(0f, true);
    }

    public void AddResearch(float amount)
    {
        if (researchBar == null) return;

        researchBar.UpdateBar(researchBar.CurrentValue + amount, UpdateAnim.Heal);

        if (!_completed && researchBar.CurrentValue >= researchBar.MaxValue)
        {
            _completed = true;
            onResearchComplete?.Invoke();
        }
    }
}
