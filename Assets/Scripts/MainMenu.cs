using System;
using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    public GameObject loadingProgress;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public async void CreateGame() {
        try
        {
            if (loadingProgress != null) { 
                
                loadingProgress.SetActive(true);
            }

            await SessionManager.Instance.CreateSession(4);
            SceneManager.LoadScene("LobbyMenu");
        }
        catch (Exception e) {
            Debug.LogError("Failed to create session: " + e.Message);
            if (loadingProgress != null) loadingProgress.SetActive(false);
        }
    
    }

    public void JoinGame(){ 
    
        SceneManager.LoadScene("JoinMenu");

    }

    public void Settings() { 
    
        SceneManager.LoadScene("SettingsMenu");
    }
    public void Quit() {

        Application.Quit();
    
    }
}
