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
        var options = new SessionOptions { MaxPlayers = maxPlayers, PlayerProperties = NameProperty() }.WithRelayNetwork();

        // Host CREATES the session (with their nickname attached).
        currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        Debug.Log("Session Code:" + currentSession.Code);
        return currentSession.Code;
    }

    public async Task JoinSession(string Code)
    {

        await WaitUntilReady();

        // Strip hidden characters/spaces and normalize case.
        string cleanCode = new string(System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(Code ?? "", char.IsLetterOrDigit))).ToUpperInvariant();

        // Player JOINS by code (with their nickname attached).
        currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(cleanCode,
            new JoinSessionOptions { PlayerProperties = NameProperty() });

    }

    public async void LeaveSession()
    {

        if (currentSession != null)
        {

            await currentSession.LeaveAsync();
            currentSession = null;

        }

    }

    // The nickname each player attaches to themselves in the session, so the lobby can show it.
    static Dictionary<string, PlayerProperty> NameProperty() => new Dictionary<string, PlayerProperty>
    {
        { "name", new PlayerProperty(PlayerNameStore.Get(), VisibilityPropertyOptions.Member) }
    };
    public ISession GetSession() => currentSession;

}