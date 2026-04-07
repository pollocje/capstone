using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Extends NetworkTransform to give authority to the owning client
/// instead of the server. This allows each player to control their
/// own position locally while keeping other clients in sync.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
