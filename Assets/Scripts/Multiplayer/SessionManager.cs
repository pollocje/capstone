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


    async void Start() {

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in anonymously with Player ID: " + AuthenticationService.Instance.PlayerId);
        }

        isReady = true;

    }

    private async Task WaitUntilReady() {

        while (!isReady) {
            await Task.Delay(100);
        }
    
    }
    void Awake() {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {

            Destroy(gameObject);
        }
    
    }

    public async Task<string> CreateSession(int maxPlayers = 4) { 
    
        await WaitUntilReady();
        var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();

        currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

        Debug.Log("Session Code:" + currentSession.Code);
        return currentSession.Code;
    }

    public async Task JoinSession(string Code) { 
    
        await WaitUntilReady();
        currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(Code);
    
    }

    public async void LeaveSession() {

        if (currentSession != null){

            await currentSession.LeaveAsync();
            currentSession = null;
        
        }
    
    }
    public ISession GetSession() => currentSession;

}
