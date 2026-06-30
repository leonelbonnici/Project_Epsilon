using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Generic "all players present" gate. Drop on any object with a trigger Collider2D.
// Server tracks which players are in the zone via a replicated NetworkList<ulong>.
// When every currently-connected player is simultaneously inside, fires onAllReady
// (server-side). Wire onAllReady in the inspector to any public no-arg method —
// ScenePortal.ServerInitiateTransition, ArenaBridge.ServerStartEncounter, a door
// open method, whatever.
[RequireComponent(typeof(NetworkObject))]
public class ReadyZone : NetworkBehaviour, IInteractable
{
    [UnityEngine.Tooltip("If true, onAllReady fires only the first time the zone becomes all-ready. " +
        "If false, it fires every time the zone re-enters that state.")]
    public bool oneShot = true;

    [UnityEngine.Tooltip("Server-side. Invoked when every connected player is inside the zone. " +
        "Drag any GameObject and pick a public no-arg method.")]
    public UnityEvent onAllReady;

    [UnityEngine.Tooltip("Server-side. Invoked when the zone leaves the all-ready state (a player " +
        "stepped out, disconnected, or a new player joined). Optional — useful for repeatable gates.")]
    public UnityEvent onAllReadyLost;

    [UnityEngine.Tooltip("Server-side. Fires when any player presses Q while the zone is armed (all players present). This is the primary gated action — wire ServerStartEncounter, ServerInitiateTransition, etc. here.")]
    public UnityEvent onActivate;

    public bool IsAvailable => currentlyAllReady.Value && (!oneShot || !hasFired.Value);

    private NetworkList<ulong> readyClients = new NetworkList<ulong>();

    private NetworkVariable<bool> currentlyAllReady = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> hasFired = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> totalConnected = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int ReadyCount => readyClients != null ? readyClients.Count : 0;
    public int TotalCount => totalConnected.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientChanged;
        }
    }

    private void OnClientChanged(ulong clientId)
    {
        if (!IsServer) return;

        // Disconnects don't fire OnTriggerExit2D — purge stale entries here.
        if (readyClients.Contains(clientId) && !IsClientStillConnected(clientId))
        {
            readyClients.Remove(clientId);
        }
        EvaluateReady();
    }

    private bool IsClientStillConnected(ulong clientId)
    {
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            if (c.ClientId == clientId) return true;
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (!TryGetClientId(other, out ulong clientId)) return;
        if (!readyClients.Contains(clientId)) readyClients.Add(clientId);
        EvaluateReady();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) return;
        if (!TryGetClientId(other, out ulong clientId)) return;
        readyClients.Remove(clientId);
        EvaluateReady();
    }

    private bool TryGetClientId(Collider2D other, out ulong clientId)
    {
        clientId = 0;
        var bridge = other.GetComponentInParent<NetworkPlayMakerBridge>();
        if (bridge == null) return false;
        clientId = bridge.OwnerClientId;
        return true;
    }

    private void EvaluateReady()
    {
        if (!IsServer) return;

        int total = NetworkManager.Singleton.ConnectedClientsList.Count;
        totalConnected.Value = total;

        int ready = readyClients.Count;
        bool allReady = total > 0 && ready >= total;

        if (allReady && !currentlyAllReady.Value)
        {
            currentlyAllReady.Value = true;
            onAllReady?.Invoke();
        }
        else if (!allReady && currentlyAllReady.Value)
        {
            currentlyAllReady.Value = false;
            onAllReadyLost?.Invoke();
        }
    }

    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        if (!IsAvailable) return;

        if (oneShot) hasFired.Value = true;
        onActivate?.Invoke();
    }
}