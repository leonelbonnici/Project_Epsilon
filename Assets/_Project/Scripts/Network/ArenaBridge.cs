using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative arena: tracks encounter status, spawns the boss, and broadcasts
// PlayMaker events to its FSMs for the encounter flow.
public class ArenaBridge : NetworkBehaviour, IRoom, IPersistable
{
    [Header("Party wipe")]

    [UnityEngine.Tooltip("HP fraction (0-1) given to revived players after a party wipe. 1 = full HP. Keep > 0, otherwise they arrive in the hub still downed and unable to interact.")]
    [Range(0f, 1f)]
    public float reviveOnWipeFraction = 0.5f;
    private bool wipeTriggered = false;
    private readonly List<NetworkPlayMakerBridge> subscribedBridges = new();

    [UnityEngine.Tooltip("Fired to FSMs when a party wipe begins. Hook this for cutscene/effect/sound (screen flash, fade-to-black, camera shake, defeat jingle, etc).")]
    public string WipedEvent = "ARENA_WIPED";

    [UnityEngine.Tooltip("Seconds to wait after a party wipe before transitioning to the hub. Gives the WIPED-event cutscene time to play out.")]
    [Range(0f, 10f)]
    public float wipeToHubDelay = 2f;

    [UnityEngine.Tooltip("Scene name to send players to after a party wipe.")]
    public string wipeReturnScene = "Hub";

    [UnityEngine.Tooltip("Spawn point name in the wipe-return scene.")]
    public string wipeReturnSpawnPoint = "PlayerSpawn_HubArrival";

    [UnityEngine.Tooltip("Health fraction (0-1) restored to downed players when the boss dies. Set to 0 to disable auto-revive for this arena.")]
    [Range(0f, 1f)]
    public float reviveOnBossDeathFraction = 0.25f;

    [UnityEngine.Tooltip("Altar that drops at the boss's death position. Replaces the old cleared marker. Optional — leave null if this arena doesn't have one.")]
    public EndAreaAltar dropAltar;

    // --- IPersistable ---
    public string PersistenceId => $"arena:{roomId}";

    public string CaptureState() => status.Value == (int)Status.Cleared ? "1" : "0";

    public void RestoreState(string state)
    {
        // Only the "Cleared" state is meaningful to persist. Anything else
        // (InProgress, Failed) resets to Idle so the player can re-attempt.
        bool wasCleared = state == "1";
        status.Value = wasCleared ? (int)Status.Cleared : (int)Status.Idle;
    }

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
        wipeTriggered = false;
        SubscribeToPartyDowned();
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
        UnsubscribeFromPartyDowned();

        Vector3 deathPos = spawnedBoss != null ? spawnedBoss.transform.position : transform.position;
        if (spawnedBoss != null) spawnedBoss.DiedRaised -= OnBossDied;

        if (dropAltar != null)
        {
            dropAltar.ServerDropAtPosition(deathPos);
        }

        status.Value = (int)Status.Cleared;
        BroadcastEventRpc(ClearedEvent);
        RoomCompleted?.Invoke(this);

        // Auto-revive any downed players at the configured HP fraction.
        ReviveDownedPlayers();
    }

    private void ReviveDownedPlayers()
    {
        // Boss-death revive — preserves the existing 3c behaviour.
        ReviveAllDownedPlayers(reviveOnBossDeathFraction);
    }

    private void ReviveAllDownedPlayers(float hpFraction)
    {
        if (!IsServer) return;
        if (hpFraction <= 0f) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge == null) continue;
            if (!bridge.IsDowned) continue;

            float reviveHP = bridge.maxHealth * hpFraction;
            bridge.ServerRevive(reviveHP);
        }
    }

    private void SubscribeToPartyDowned()
    {
        UnsubscribeFromPartyDowned(); // safety against double-subscribe on weird re-entries

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge == null) continue;

            bridge.DownedChanged += OnPlayerDownedChanged;
            subscribedBridges.Add(bridge);
        }
    }

    private void UnsubscribeFromPartyDowned()
    {
        foreach (var bridge in subscribedBridges)
        {
            if (bridge != null) bridge.DownedChanged -= OnPlayerDownedChanged;
        }
        subscribedBridges.Clear();
    }

    private void OnPlayerDownedChanged(bool nowDowned)
    {
        if (!IsServer) return;
        if (!nowDowned) return;                              // ignore "got revived" transitions
        if (wipeTriggered) return;                           // idempotent — fire once per encounter
        if (status.Value != (int)Status.InProgress) return;  // ignore stale callbacks during teardown

        if (AreAllConnectedPlayersDowned())
        {
            wipeTriggered = true;
            TriggerPartyWipe();
        }
    }

    private bool AreAllConnectedPlayersDowned()
    {
        int considered = 0;
        int downed = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge == null) continue;

            considered++;
            if (bridge.IsDowned) downed++;
        }

        return considered > 0 && downed == considered;
    }

    private void TriggerPartyWipe()
    {
        if (!IsServer) return;

        // Stop listening before we mutate anything else.
        UnsubscribeFromPartyDowned();

        // Detach from the live boss before the scene unload destroys it.
        if (spawnedBoss != null) spawnedBoss.DiedRaised -= OnBossDied;

        // Notify FSMs so any cutscene/effect can start now.
        BroadcastEventRpc(WipedEvent);

        // Defer the rest of the wipe until after the cutscene window.
        StartCoroutine(CompleteWipeAfterDelay());
    }

    private System.Collections.IEnumerator CompleteWipeAfterDelay()
    {
        if (wipeToHubDelay > 0f) yield return new WaitForSeconds(wipeToHubDelay);

        // Revive everyone so they're playable in the hub.
        // (Done AFTER the delay so players stay visually downed during the "defeat" moment.)
        ReviveAllDownedPlayers(reviveOnWipeFraction);

        // Only THIS arena resets. IPersistable.CaptureState only persists 'Cleared',
        // so Idle is the live default again on re-entry — boss respawns fresh.
        status.Value = (int)Status.Idle;

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.ServerTransitionToScene(wipeReturnScene, wipeReturnSpawnPoint);
        }
        else
        {
            Debug.LogError("[ArenaBridge] SceneFlowController not found; party wipe cannot complete the hub transition.");
        }
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
        UnsubscribeFromPartyDowned();
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