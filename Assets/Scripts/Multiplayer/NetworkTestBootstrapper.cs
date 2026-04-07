using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading;

/// <summary>
/// Local-only bootstrapper for testing multiplayer directly in a game scene
/// without going through the UGS lobby flow.
///
/// Uses a named OS Mutex so the first virtual player to reach Start() atomically
/// claims the host role — no race condition, no NGO error/shutdown path.
///
/// Remove or disable this component before shipping.
/// </summary>
public class NetworkTestBootstrapper : MonoBehaviour
{
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort serverPort = 7777;

    private const string HostMutexName = "UnityMPPM_LocalTestHost";
    private static Mutex _hostMutex;

    void Start()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening) return;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
            transport.SetConnectionData(serverAddress, serverPort);

        if (TryClaimHost())
            NetworkManager.Singleton.StartHost();
        else
            NetworkManager.Singleton.StartClient();
    }

    static bool TryClaimHost()
    {
        _hostMutex = new Mutex(initiallyOwned: true, name: HostMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another virtual player already owns the mutex — we are a client
            _hostMutex.Dispose();
            _hostMutex = null;
        }
        return createdNew;
    }

    void OnDestroy()
    {
        if (_hostMutex != null)
        {
            _hostMutex.ReleaseMutex();
            _hostMutex.Dispose();
            _hostMutex = null;
        }
    }
}
