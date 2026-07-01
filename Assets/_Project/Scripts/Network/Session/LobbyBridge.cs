using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// A slot in the lobby / party roster. Persists across a disconnect — if a player
// leaves, their entry stays but 'connected' flips false; if they rejoin (same session),
// the server matches them back to their slot instead of adding a new row.
public struct PartySlot : INetworkSerializable, System.IEquatable<PartySlot>
{
    public ulong clientId;
    public FixedString32Bytes name;
    public bool ready;
    public bool connected;  // false while awaiting a reconnect

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref clientId);
        s.SerializeValue(ref name);
        s.SerializeValue(ref ready);
        s.SerializeValue(ref connected);
    }

    // Slot identity is by name (persistent across ClientId churn on reconnect).
    // If we later add a stable player identity (Unity Auth ID, Steam ID), swap that in.
    public bool Equals(PartySlot other) => name.Equals(other.name);
}

public class LobbyBridge : NetworkBehaviour
{
    public static LobbyBridge Instance { get; private set; }

    [UnityEngine.Tooltip("Party roster. Slots persist across disconnects (connected=false while awaiting reconnect).")]
    public NetworkList<PartySlot> Slots;

    public NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [UnityEngine.Tooltip("Minimum ready players required to start the game.")]
    public int minPlayersToStart = 1;

    [UnityEngine.Tooltip("Maximum party size.")]
    public int maxPlayers = 3;

    [UnityEngine.Tooltip("Scene to transition to when Start Game fires.")]
    public string firstGameplayScene = "Hub";

    [UnityEngine.Tooltip("Spawn point name in the first gameplay scene.")]
    public string firstSpawnPoint = "PlayerSpawn_HubArrival";

    private void Awake()
    {
        Slots = new NetworkList<PartySlot>();
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Add the host as the first slot.
            AddOrReclaimSlot(NetworkManager.Singleton.LocalClientId, $"Player {NetworkManager.Singleton.LocalClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // --- Server-side slot management ---

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return; // host already added
        AddOrReclaimSlot(clientId, $"Player {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].clientId != clientId) continue;

            if (GameStarted.Value)
            {
                // Game in progress: keep the slot, mark as disconnected — awaiting reconnect.
                var s = Slots[i];
                s.connected = false;
                Slots[i] = s;
            }
            else
            {
                // Still in lobby: just remove the slot outright.
                Slots.RemoveAt(i);
            }
            return;
        }
    }

    private void AddOrReclaimSlot(ulong clientId, string defaultName)
    {
        // Reconnection: look for an existing slot by name that's currently disconnected.
        // NOTE: matching by name is a placeholder — swap to Unity Auth playerId later.
        for (int i = 0; i < Slots.Count; i++)
        {
            if (!Slots[i].connected && Slots[i].name.ToString() == defaultName)
            {
                var s = Slots[i];
                s.clientId = clientId;      // new ClientId post-reconnect
                s.connected = true;
                s.ready = false;             // require re-ready after reconnect
                Slots[i] = s;
                return;
            }
        }

        // New player: add a fresh slot.
        if (Slots.Count >= maxPlayers) return;
        Slots.Add(new PartySlot
        {
            clientId = clientId,
            name = defaultName,
            ready = false,
            connected = true
        });
    }

    // --- Client-driven RPCs ---

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].clientId == senderId)
            {
                var updated = Slots[i];
                updated.ready = ready;
                Slots.RemoveAt(i);
                Slots.Insert(i, updated);
                return;
            }
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestStartGameRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != NetworkManager.ServerClientId) return;
        if (GameStarted.Value) return;
        if (Slots.Count < minPlayersToStart) return;
        foreach (var s in Slots)
            if (s.connected && !s.ready) return;

        GameStarted.Value = true;

        // Spawn a PlayerObject for every connected client before the scene transition.
        SpawnAllPlayers();

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.ServerTransitionToScene(firstGameplayScene, firstSpawnPoint);
        }
        else
        {
            Debug.LogError("[LobbyBridge] SceneFlowController not found; cannot transition to gameplay scene.");
        }
    }

    private void SpawnAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("[LobbyBridge] NetworkManager or PlayerPrefab missing; cannot spawn players.");
            return;
        }

        foreach (var client in nm.ConnectedClientsList)
        {
            // Skip if this client already has a PlayerObject (shouldn't happen with CreatePlayerObject=false, but defensive).
            if (client.PlayerObject != null) continue;

            var playerInstance = Instantiate(nm.NetworkConfig.PlayerPrefab);
            var netObj = playerInstance.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(client.ClientId);
        }
    }

    // --- Reconnection gate: server-only query used by SessionGuard ---

    // Returns true if the current session state allows a reconnecting client to be admitted.
    // Policy: reconnect allowed only when the party is back in the Hub, not mid-encounter or transition.
    public bool IsReconnectAllowedNow()
    {
        if (!IsServer) return false;
        if (SceneFlowController.Instance == null) return false;

        // Refuse mid-transition
        if (SceneFlowController.Instance.IsTransitioning) return false;

        // Refuse if not in the Hub
        if (SceneFlowController.Instance.CurrentGameplayScene != firstGameplayScene) return false;

        return true;
    }

    // Returns true if 'name' matches a disconnected slot (i.e. this is a genuine reconnect).
    public bool HasDisconnectedSlotFor(string name)
    {
        if (!IsServer) return false;
        foreach (var s in Slots)
            if (!s.connected && s.name.ToString() == name) return true;
        return false;
    }

    // --- Client-side helpers ---

    public bool IsLocalPlayerReady()
    {
        if (Slots == null) return false;
        ulong id = NetworkManager.Singleton.LocalClientId;
        foreach (var s in Slots)
            if (s.clientId == id) return s.ready;
        return false;
    }

    public bool AllConnectedPlayersReady()
    {
        if (Slots == null || Slots.Count < minPlayersToStart) return false;
        foreach (var s in Slots)
            if (s.connected && !s.ready) return false;
        return true;
    }

    public bool IsLocalPlayerHost() =>
        NetworkManager.Singleton.LocalClientId == NetworkManager.ServerClientId;

    public int ConnectedCount()
    {
        int count = 0;
        if (Slots == null) return 0;
        foreach (var s in Slots) if (s.connected) count++;
        return count;
    }
}