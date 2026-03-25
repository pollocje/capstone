using UnityEngine;

public class Debugtesthealth : MonoBehaviour
{
    private UIManager uiManager;

    void Start()
    {
        uiManager = FindObjectOfType<UIManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            uiManager.Damage();
        }
    }
}
