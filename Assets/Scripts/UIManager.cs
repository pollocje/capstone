using Microlight.MicroBar;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private MicroBar healthBar;

    private void Start()
    {
        if (healthBar == null)
        {
            Debug.LogError("UIManager: healthBar is not assigned.", this);
            return;
        }

        healthBar.Initialize(100f);
    }

    public void Damage()
    {
        if (healthBar == null)
        {
            Debug.LogError("UIManager: healthBar is not assigned.", this);
            return;
        }

        healthBar.UpdateBar(healthBar.CurrentValue - 10f);
    }
}