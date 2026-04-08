using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using System.Collections;
using Unity.Services.Lobbies;
public class LobbyMenu : MonoBehaviour
{
    public TextMeshProUGUI lobbyCodeText;

    public TextMeshProUGUI teammate1;
    public TextMeshProUGUI teammate2;
    public TextMeshProUGUI teammate3;
    public TextMeshProUGUI teammate4;

    public GameObject startGameButton;

    private TextMeshProUGUI[] teammateTexts;
    private ISession session;

    void Start()
    {
        teammateTexts = new TextMeshProUGUI[] { teammate1, teammate2, teammate3, teammate4 };
        session = SessionManager.Instance.GetSession();

        if (session == null) { 
            
            Debug.LogError("No session found. Returning to main menu.");
            return;
        }

        lobbyCodeText.text = "Code: " + session.Code; ;

        startGameButton.SetActive(NetworkManager.Singleton.IsHost);

        StartCoroutine(RefreshPlayerList());
    }

    IEnumerator RefreshPlayerList()
    {

        while (true) {

            UpdatePlayerText();
            yield return new WaitForSeconds(1f); // Refresh every second

        }


    }

    void UpdatePlayerText() { 
        foreach(var slot in teammateTexts){

            slot.text = "Waiting for player...";
        }

        int i = 0;

        foreach(var player in session.Players) { 
            if (i < teammateTexts.Length) { 
                teammateTexts[i].text = player.Id.Substring(0,5);
                i++;
            }
        }

    }
    public void StartGame()
    {

        if (NetworkManager.Singleton.IsHost) { 
        
          NetworkManager.Singleton.SceneManager.LoadScene("Prototype_World", LoadSceneMode.Single);

        }


    }


    public void ExitLobby()
    {
        StopAllCoroutines();
        SessionManager.Instance.LeaveSession();
        SceneManager.LoadScene("MainMenu");
    }

}
