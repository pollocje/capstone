using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using System.Collections;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

    void UpdatePlayerText()
    {
        foreach (var slot in teammateTexts)
            slot.text = "Waiting for player...";

        try
        {
            // Snapshot IDs first to avoid iterating a collection modified on a background thread
            var playerIds = new System.Collections.Generic.List<string>();
            foreach (var player in session.Players)
                playerIds.Add(player.Id);

            for (int i = 0; i < playerIds.Count && i < teammateTexts.Length; i++)
            {
                var id = playerIds[i];
                teammateTexts[i].text = id.Length >= 5 ? id.Substring(0, 5) : id;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("UpdatePlayerText error: " + e.Message);
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
