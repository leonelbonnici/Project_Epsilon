using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative arena: tracks encounter status, spawns the boss, and broadcasts
// PlayMaker events to its FSMs for the encounter flow.
public class ArenaBridge : NetworkBehaviour, IRoom
{
    [UnityEngine.Tooltip("Unique identifier for this room within its area. Used by doors to declare prerequisites.")]
    public string roomId = "arena_01";

    // --- IRoom implementation ---
    public string RoomId => roomId;
    public bool IsCompleted => status.Value == (int)Status.Cleared;
    public event System.Action<IRoom> RoomCompleted;

    public enum Status { Idle = 0, InProgress = 1, Cleared = 2, Failed = 3 }

    [UnityEngine.Tooltip("Boss prefab to spawn. Must be in the NetworkPrefabs list.")]
    public GameObject bossPrefab;

    [UnityEngine.Tooltip("Where the boss spawns. If null, uses this arena's position.")]
    public Transform bossSpawnPoint;

    [UnityEngine.Tooltip("Fired when the arena is network-ready.")]
    public string SpawnEvent = "ARENA_SPAWNED";
    [UnityEngine.Tooltip("Fired when the encounter starts.")]
    public string StartedEvent = "ARENA_STARTED";
    [UnityEngine.Tooltip("Fired when the boss has spawned.")]
    public string BossSpawnedEvent = "ARENA_BOSS_SPAWNED";
    [UnityEngine.Tooltip("Fired when the encounter ends in victory.")]
    public string ClearedEvent = "ARENA_CLEARED";
    [UnityEngine.Tooltip("Fired when the encounter ends in defeat (later phases).")]
    public string FailedEvent = "ARENA_FAILED";

    private NetworkVariable<int> status = new NetworkVariable<int>(
        (int)Status.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int StatusValue => status.Value;
    public bool IsServerArena => IsServer;

    // Server-only handle to the spawned boss (so we can subscribe to its death).
    private BossBridge spawnedBoss;

    // Called from the arena's FSM on the server when players are ready.
    public void ServerStartEncounter()
    {
        if (!IsServer) return;
        if (status.Value != (int)Status.Idle) return;       // already running or done

        status.Value = (int)Status.InProgress;
        SpawnBoss();
        BroadcastEventRpc(StartedEvent);
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null) return;
        Vector3 pos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;

        GameObject obj = Instantiate(bossPrefab, pos, Quaternion.identity);
        spawnedBoss = obj.GetComponent<BossBridge>();

        // Scale HP by player count BEFORE spawning — BossBridge.OnNetworkSpawn reads maxHealth on the server.
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (spawnedBoss != null) spawnedBoss.maxHealth *= Mathf.Max(1, playerCount);

        obj.GetComponent<NetworkObject>().Spawn();

        // Cross-object glue: arena listens for the boss's death and turns it into ARENA_CLEARED.
        if (spawnedBoss != null) spawnedBoss.DiedRaised += OnBossDied;

        BroadcastEventRpc(BossSpawnedEvent);
    }

    private void OnBossDied()
    {
        if (!IsServer) return;
        if (spawnedBoss != null) spawnedBoss.DiedRaised -= OnBossDied;
        status.Value = (int)Status.Cleared;
        BroadcastEventRpc(ClearedEvent);
        RoomCompleted?.Invoke(this);
    }

    public override void OnNetworkSpawn()
    {
        status.OnValueChanged += HandleStatusChanged;
        SendEventToAllFsms(SpawnEvent);
    }

    public override void OnNetworkDespawn()
    {
        status.OnValueChanged -= HandleStatusChanged;
        if (spawnedBoss != null) spawnedBoss.DiedRaised -= OnBossDied;
    }

    private void HandleStatusChanged(int prev, int curr) { /* reserved for later UI hooks */ }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEventRpc(Unity.Collections.FixedString64Bytes e) => SendEventToAllFsms(e.ToString());

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }
}