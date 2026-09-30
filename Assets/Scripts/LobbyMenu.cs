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

    [Tooltip("How long to wait for the join to finish before giving up.")]
    public float sessionWaitTimeout = 10f;

    private TextMeshProUGUI[] teammateTexts;
    private ISession session;

    void Start()
    {
        teammateTexts = new TextMeshProUGUI[] { teammate1, teammate2, teammate3, teammate4 };

        // Hide host-only controls until we know who we are.
        startGameButton.SetActive(false);
        lobbyCodeText.text = "Code: ...";

        StartCoroutine(WaitForSessionThenSetup());
    }

    IEnumerator WaitForSessionThenSetup()
    {
        // Clients can arrive here (via the host's scene sync) slightly before
        // JoinSessionByCodeAsync has finished, so wait for the session to be set.
        float timer = sessionWaitTimeout;
        while (SessionManager.Instance.GetSession() == null && timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            yield return null;
        }

        session = SessionManager.Instance.GetSession();

        if (session == null)
        {
            Debug.LogError("No session found. Returning to main menu.");
            yield break;
        }

        lobbyCodeText.text = "Code: " + session.Code;
        startGameButton.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);

        StartCoroutine(RefreshPlayerList());
    }

    IEnumerator RefreshPlayerList()
    {
        while (true)
        {
            UpdatePlayerText();
            yield return new WaitForSeconds(1f); // Refresh every second
        }
    }

    void UpdatePlayerText()
    {
        foreach (var slot in teammateTexts)
        {
            slot.text = "Waiting for player...";
        }

        int i = 0;
        foreach (var player in session.Players)
        {
            if (i < teammateTexts.Length)
            {
                teammateTexts[i].text = player.Id.Substring(0, 5);
                i++;
            }
        }
    }

    public void StartGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
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