using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    public void CreateGame() {

        SceneManager.LoadScene("LobbyMenu");
    
    
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
