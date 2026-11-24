using UnityEngine;
using Unity.Netcode;

public class LocalHostBootstrap : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.StartHost();
    }
}
