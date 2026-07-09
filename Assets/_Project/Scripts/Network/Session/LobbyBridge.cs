using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using Unity.Services.Multiplayer;
using System;

public enum SlotState : byte
{
    InLobby = 0,
    Playing = 1,
    Disconnected = 2,
    WaitingToJoin = 3
}

// Bridges SessionGuard's approval payload to LobbyBridge's OnClientConnected.
// SessionGuard captures the incoming UGS PlayerId at approval; LobbyBridge reads
// it moments later when the client is fully connected.
public static class PendingPlayerIds
{
    private static readonly Dictionary<ulong, string> pending = new();

    public static void Stash(ulong clientId, string playerId)
    {
        pending[clientId] = playerId;
    }

    public static string Consume(ulong clientId)
    {
        if (pending.TryGetValue(clientId, out var id))
        {
            pending.Remove(clientId);
            return id;
        }
        return "";
    }
}

// A slot in the lobby / party roster. Persists across a disconnect — if a player
// leaves, their entry stays but 'connected' flips false; if they rejoin (same session),
// the server matches them back to their slot instead of adding a new row.
public struct PartySlot : INetworkSerializable, System.IEquatable<PartySlot>
{
    public ulong clientId;                     // current connection ID (churns on reconnect)
    public FixedString64Bytes playerId;         // UGS Auth PlayerId (stable across the session)
    public FixedString32Bytes name;
    public bool ready;
    public SlotState state;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref clientId);
        s.SerializeValue(ref playerId);
        s.SerializeValue(ref name);
        s.SerializeValue(ref ready);
        s.SerializeValue(ref state);
    }

    // Identity is by UGS PlayerId (stable across reconnect).
    public bool Equals(PartySlot other) => playerId.Equals(other.playerId);
}

public class LobbyBridge : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> CurrentSceneReplicated = 
        new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

            // Add the host as the first slot (their own OnClientConnected fires before
            // this OnNetworkSpawn, so we can't rely on it — add inline).
            ulong hostId = NetworkManager.Singleton.LocalClientId;
            string hostPlayerId = PendingPlayerIds.Consume(hostId);
            if (string.IsNullOrEmpty(hostPlayerId)) hostPlayerId = $"host-{hostId}";

            Slots.Add(new PartySlot
            {
                clientId = hostId,
                playerId = hostPlayerId,
                name = $"Player {hostId}",
                ready = false,
                state = SlotState.InLobby
            });

            if (SceneFlowController.Instance != null)
            {
                SceneFlowController.Instance.SceneLoadCompleted += OnSceneLoadCompleted;
            }
        }
    }

    private void OnSceneLoadCompleted(string sceneName)
    {
        if (!IsServer) return;
        
        Debug.Log($"[Lobby] OnSceneLoadCompleted: sceneName={sceneName}, writing to CurrentSceneReplicated");

        CurrentSceneReplicated.Value = sceneName;
        if (sceneName != firstGameplayScene) return;   // only care about Hub arrivals

        // Admit any waiting slots
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].state != SlotState.WaitingToJoin) continue;

            var s = Slots[i];
            s.state = SlotState.Playing;
            Slots.RemoveAt(i);
            Slots.Insert(i, s);
            SpawnPlayerObject(s.clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.SceneLoadCompleted -= OnSceneLoadCompleted;
        }
        if (Instance == this) Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Server-side slot management ---

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // The host's own connection is added via OnNetworkSpawn, not here.
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        string playerId = GetPlayerIdForClient(clientId);
        if (string.IsNullOrEmpty(playerId)) return;   // shouldn't happen — SessionGuard rejects payloadless

        // Reconnect path
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].playerId.ToString() == playerId &&
                (Slots[i].state == SlotState.Disconnected))
            {
                HandleReconnect(i, clientId);
                return;
            }
        }

        // Fresh join to pre-game lobby
        if (GameStarted.Value) return;   // shouldn't happen — SessionGuard rejects
        if (Slots.Count >= maxPlayers) return;

        var slot = new PartySlot
        {
            clientId = clientId,
            playerId = playerId,
            name = $"Player {clientId}",
            ready = false,
            state = SlotState.InLobby
        };
        Slots.Add(slot);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || !IsSpawned) return;

        // Do the authoritative slot mutation FIRST, synchronously, while we still
        // definitely hold write authority. The old code awaited a UGS call before
        // writing to Slots — during host teardown, authority vanished across that
        // await, causing "Client is not allowed to write to this NetworkList".
        string playerIdToRemove = "";
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].clientId != clientId) continue;

            playerIdToRemove = Slots[i].playerId.ToString();

            var s = Slots[i];
            if (GameStarted.Value)
            {
                s.state = SlotState.Disconnected;
                Slots.RemoveAt(i);
                Slots.Insert(i, s);
                Debug.Log($"[Lobby] Marked slot {i} (clientId {clientId}) as Disconnected");
            }
            else
            {
                Slots.RemoveAt(i);
            }
            break;
        }

        // THEN free their UGS session slot so they can rejoin later. Fire-and-forget:
        // it's independent of the roster, has its own try/catch, and if we're mid-
        // teardown any "lobby not found" it throws is swallowed there.
        if (!string.IsNullOrEmpty(playerIdToRemove))
        {
            _ = RemovePlayerFromUgsSession(playerIdToRemove);
        }
    }

    private async System.Threading.Tasks.Task RemovePlayerFromUgsSession(string playerId)
    {
        try
        {
            var sessionSettings = MultiplayerSessionManager.Instance?.sessionSettings;
            if (sessionSettings == null) return;

            var session = Unity.Services.Multiplayer.MultiplayerService.Instance?.Sessions
                .GetValueOrDefault(sessionSettings.sessionType);
            if (session is Unity.Services.Multiplayer.IHostSession hostSession)
            {
                await hostSession.AsHost().RemovePlayerAsync(playerId);
                Debug.Log($"[Lobby] Removed player {playerId} from UGS session (allows reconnect)");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lobby] Failed to remove player from UGS session: {e.Message}");
        }
    }

    private void HandleReconnect(int slotIndex, ulong newClientId)
    {
        if (!IsServer) return;

        var s = Slots[slotIndex];
        s.clientId = newClientId;

        bool inHub = IsPartyInHub();
        Debug.Log($"[Lobby] HandleReconnect slotIndex={slotIndex} newClientId={newClientId} inHub={inHub} currentScene='{SceneFlowController.Instance?.CurrentGameplayScene}' isTransitioning={SceneFlowController.Instance?.IsTransitioning}");
        if (inHub)
        {
            s.state = SlotState.Playing;
            Slots.RemoveAt(slotIndex);
            Slots.Insert(slotIndex, s);
            SpawnPlayerObject(newClientId);
        }
        else
        {
            s.state = SlotState.WaitingToJoin;
            Slots.RemoveAt(slotIndex);
            Slots.Insert(slotIndex, s);
            // No PlayerObject spawn. Waiting room UI on the client picks up the state.
        }
    }

    private bool IsPartyInHub()
    {
        if (SceneFlowController.Instance == null) return false;
        if (SceneFlowController.Instance.IsTransitioning) return false;
        return SceneFlowController.Instance.CurrentGameplayScene == firstGameplayScene;
    }

    private string GetPlayerIdForClient(ulong clientId)
    {
        // In this NGO setup, the payload for the incoming client is captured by SessionGuard
        // but not stored anywhere accessible per-client here. Simplest fix: SessionGuard also
        // stashes the last-approved payload in a static dictionary that LobbyBridge reads.
        return PendingPlayerIds.Consume(clientId);
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
            if (s.state == SlotState.InLobby && !s.ready) return;

        GameStarted.Value = true;

        // Flip all InLobby slots to Playing
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].state != SlotState.InLobby) continue;
            var s = Slots[i];
            s.state = SlotState.Playing;
            Slots.RemoveAt(i);
            Slots.Insert(i, s);
        }

        SpawnAllPlayers();
        SceneFlowController.Instance.ServerTransitionToScene(firstGameplayScene, firstSpawnPoint);
    }

    private void SpawnPlayerObject(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("[LobbyBridge] Cannot spawn — NetworkManager or PlayerPrefab missing.");
            return;
        }

        // If they already have one (shouldn't happen), skip
        if (nm.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null) return;

        var instance = Instantiate(nm.NetworkConfig.PlayerPrefab);
        var netObj = instance.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);
    }

    private void SpawnAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        foreach (var client in nm.ConnectedClientsList)
        {
            SpawnPlayerObject(client.ClientId);
        }
    }

    public bool HasReservedSlotFor(string playerId)
    {
        if (!IsServer) return false;
        if (string.IsNullOrEmpty(playerId)) return false;
        foreach (var s in Slots)
        {
            Debug.Log($"[Lobby] HasReservedSlotFor checking: incoming='{playerId}' vs slot playerId='{s.playerId}' state={s.state}");
            if (s.playerId.ToString() == playerId && s.state == SlotState.Disconnected) return true;
        }
        return false;
    }

    // --- Reconnection gate: server-only query used by SessionGuard ---

    // --- Client-side helpers ---
    public bool IsLocalPlayerHost() =>
    NetworkManager.Singleton.LocalClientId == NetworkManager.ServerClientId;

    public bool IsLocalPlayerReady()
    {
        if (Slots == null) return false;
        ulong id = NetworkManager.Singleton.LocalClientId;
        foreach (var s in Slots)
            if (s.clientId == id) return s.ready;
        return false;
    }

    public int ConnectedCount()
    {
        int count = 0;
        if (Slots == null) return 0;
        foreach (var s in Slots) if (s.state == SlotState.InLobby || s.state == SlotState.Playing) count++;
        return count;
    }

    public bool AllConnectedPlayersReady()
    {
        if (Slots == null || Slots.Count < minPlayersToStart) return false;
        foreach (var s in Slots)
            if (s.state == SlotState.InLobby && !s.ready) return false;
        return true;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void HostKickSlotRpc(int slotIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != NetworkManager.ServerClientId) return;   // host only
        if (slotIndex < 0 || slotIndex >= Slots.Count) return;

        var s = Slots[slotIndex];
        // Only kick slots that aren't actively playing
        if (s.state != SlotState.Disconnected && s.state != SlotState.WaitingToJoin) return;

        // If they're WaitingToJoin, they're currently connected — disconnect them cleanly
        if (s.state == SlotState.WaitingToJoin)
        {
            NetworkManager.Singleton.DisconnectClient(s.clientId, "Removed from party by host.");
        }

        Slots.RemoveAt(slotIndex);
    }
}