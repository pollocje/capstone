using Microlight.MicroBar;
using UnityEngine;

public class UIManager : MonoBehaviour
{

    [SerializeField] MicroBar healthBar;
    void Start()
    {
        healthBar.Initialize(100f);
    }

    public void Damage() { 
    
        healthBar.UpdateBar(healthBar.CurrentValue - 10f);

    }

    
}
