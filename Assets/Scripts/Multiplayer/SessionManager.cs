using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : MonoBehaviour
{

    public static SessionManager Instance;


    private ISession currentSession;

    private bool isReady = false;


    async void Start()
    {

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in anonymously with Player ID: " + AuthenticationService.Instance.PlayerId);
        }

        isReady = true;

    }

    private async Task WaitUntilReady()
    {

        while (!isReady)
        {
            await Task.Delay(100);
        }

    }
    void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {

            Destroy(gameObject);
        }

    }

    public async Task<string> CreateSession(int maxPlayers = 4)
    {

        await WaitUntilReady();
        await LeaveOldSession(); // clear any previous session first

        var options = new SessionOptions { MaxPlayers = maxPlayers, PlayerProperties = NameProperty() }.WithRelayNetwork();

        // Host CREATES the session (with their nickname attached).
        currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        Debug.Log("Session Code:" + currentSession.Code);
        return currentSession.Code;
    }

    public async Task JoinSession(string Code)
    {

        await WaitUntilReady();
        await LeaveOldSession(); // clear any previous session first, so the lobby can't show its old code

        // Strip hidden characters/spaces and normalize case.
        string cleanCode = new string(System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(Code ?? "", char.IsLetterOrDigit))).ToUpperInvariant();

        // Player JOINS by code (with their nickname attached).
        currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(cleanCode,
            new JoinSessionOptions { PlayerProperties = NameProperty() });

    }

    public async void LeaveSession()
    {

        await LeaveOldSession();

    }

    // Leaves the current session (if any) and clears it straight away, so nothing
    // can read the old session while the leave request is still in progress.
    private async Task LeaveOldSession()
    {
        if (currentSession == null) return;

        ISession old = currentSession;
        currentSession = null;

        try
        {
            await old.LeaveAsync();
        }
        catch (System.Exception e)
        {
            // Usually means the session already closed (e.g. the host left). Safe to ignore.
            Debug.LogWarning("Leaving old session failed: " + e.Message);
        }
    }

    // The nickname each player attaches to themselves in the session, so the lobby can show it.
    static Dictionary<string, PlayerProperty> NameProperty() => new Dictionary<string, PlayerProperty>
    {
        { "name", new PlayerProperty(PlayerNameStore.Get(), VisibilityPropertyOptions.Member) }
    };
    public ISession GetSession() => currentSession;

}