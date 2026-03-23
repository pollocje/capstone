using UnityEngine;
using UnityEngine.SceneManagement;
public class LobbyMenu : MonoBehaviour
{
    public void StartGame()
    {

        SceneManager.LoadScene("Prototype");


    }


    public void ExitLobby()
    {
        SceneManager.LoadScene("MainMenu");
    }

}
