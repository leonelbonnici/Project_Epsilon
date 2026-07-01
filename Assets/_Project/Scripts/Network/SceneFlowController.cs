using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowController : MonoBehaviour
{
    public bool IsTransitioning => state != TransitionState.Idle;
    public string CurrentGameplayScene => currentGameplayScene;

    public static SceneFlowController Instance { get; private set; }
    private bool sceneEventSubscribed = false;

    [UnityEngine.Tooltip("Name of the hub scene (must be in Build Settings).")]
    public string hubSceneName = "Hub";

    private enum TransitionState { Idle, Unloading, Loading }
    private TransitionState state = TransitionState.Idle;

    private string pendingTargetScene = "";
    private string pendingSpawnPointName = "";
    private string currentGameplayScene = "";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientStarted += OnClientStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientStarted -= OnClientStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    // Fires on the server when any client (including the host) connects.
    // Move the newly-spawned player into DDOL so they persist across all scene transitions.
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        StartCoroutine(MovePlayerToDDOL(clientId));
    }

    private System.Collections.IEnumerator MovePlayerToDDOL(ulong clientId)
    {
        // Wait one frame for NGO's auto-spawn to complete and PlayerObject to be assigned.
        yield return null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                DontDestroyOnLoad(client.PlayerObject.gameObject);
            }
        }
    }

    private void OnServerStarted()
    {
        EnsureSceneEventSubscription();
        // Hub is no longer auto-loaded on server start.
        // It's loaded by LobbyBridge.RequestStartGameRpc when the host presses Start Game.
    }

    private void OnClientStarted()
    {
        EnsureSceneEventSubscription();
    }

    private void EnsureSceneEventSubscription()
    {
        if (sceneEventSubscribed) return;
        if (NetworkManager.Singleton?.SceneManager == null) return;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        // Critical: use Additive client sync so connecting clients keep their initial Bootstrap
        // (with Main Camera) instead of having NGO unload it during sync.
        NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(UnityEngine.SceneManagement.LoadSceneMode.Additive);

        // Defensive: don't auto-unload any client scenes that weren't NGO-loaded.
        NetworkManager.Singleton.SceneManager.PostSynchronizationSceneUnloading = false;

        sceneEventSubscribed = true;
    }

    public void ServerTransitionToScene(string targetSceneName, string spawnPointName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (state != TransitionState.Idle)
        {
            Debug.LogWarning($"[SceneFlowController] Transition requested while {state}; ignoring.");
            return;
        }
        if (currentGameplayScene == targetSceneName) return;

        pendingTargetScene = targetSceneName;
        pendingSpawnPointName = spawnPointName;

        if (string.IsNullOrEmpty(currentGameplayScene)) StartLoadPhase();
        else StartUnloadPhase();
    }

    private void StartUnloadPhase()
    {
        state = TransitionState.Unloading;
        Scene current = SceneManager.GetSceneByName(currentGameplayScene);
        if (!current.IsValid())
        {
            currentGameplayScene = "";
            StartLoadPhase();
            return;
        }

        // Snapshot the current area's state before it gets unloaded
        if (AreaStateManager.Instance != null)
        {
            AreaStateManager.Instance.SnapshotArea(currentGameplayScene);
        }

        var status = NetworkManager.Singleton.SceneManager.UnloadScene(current);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[SceneFlowController] Failed to unload {currentGameplayScene}: {status}");
            state = TransitionState.Idle;
        }
    }

    private void StartLoadPhase()
    {
        state = TransitionState.Loading;
        var status = NetworkManager.Singleton.SceneManager.LoadScene(pendingTargetScene, LoadSceneMode.Additive);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[SceneFlowController] Failed to load {pendingTargetScene}: {status}");
            state = TransitionState.Idle;
        }
    }

    // Per-machine event — fires on host AND each client when scene events happen locally.
    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // When a machine COMPLETES loading a scene locally, set its active scene.
        // This runs on every machine, so each one updates its own active scene correctly.
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
        {
            if (sceneEvent.SceneName == "Bootstrap") return;

            Scene loadedScene = SceneManager.GetSceneByName(sceneEvent.SceneName);
            if (loadedScene.IsValid()) SceneManager.SetActiveScene(loadedScene);
        }

        // Server-side aggregate events — fires once on host when all clients are done.
        if (!NetworkManager.Singleton.IsServer) return;

        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            if (sceneEvent.SceneName == "Persistent") return;

            currentGameplayScene = sceneEvent.SceneName;
            state = TransitionState.Idle;
            TeleportPlayersToSpawn(pendingSpawnPointName);

            // Restore any previously-snapshotted state for this area
            if (AreaStateManager.Instance != null)
            {
                AreaStateManager.Instance.RestoreArea(sceneEvent.SceneName);
            }
        }

        if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted)
        {
            if (state != TransitionState.Unloading) return;
            currentGameplayScene = "";
            StartLoadPhase();
        }
    }

    private void TeleportPlayersToSpawn(string spawnPointName)
    {
        if (string.IsNullOrEmpty(spawnPointName)) return;

        GameObject spawnPoint = GameObject.Find(spawnPointName);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[SceneFlowController] Spawn point '{spawnPointName}' not found in active scene.");
            return;
        }

        Vector3 spawnPos = spawnPoint.transform.position;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge != null)
            {
                bridge.ServerTeleportPlayer(spawnPos);
            }
            else
            {
                // Fallback if a player object somehow lacks the bridge — direct set
                // (only reliably works for the host's own player).
                client.PlayerObject.transform.position = spawnPos;
            }
        }
    }
}