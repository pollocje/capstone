using Microlight.MicroBar;
using UnityEngine;

/// <summary>Persistent HUD bar tracking total cloud research progress.</summary>
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    [SerializeField] private MicroBar researchBar;
    [SerializeField] private float maxResearch = 100f;

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
    }
}
