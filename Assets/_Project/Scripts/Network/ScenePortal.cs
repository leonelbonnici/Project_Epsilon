using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Portal interactable that gates transitions on all players being present.
// A trigger collider detects players in the "ready zone"; when all are in, the portal is armed.
[RequireComponent(typeof(NetworkObject))]
public class ScenePortal : NetworkBehaviour
{
    [UnityEngine.Tooltip("Scene to transition to (must be in Build Settings).")]
    public string targetSceneName = "";

    [UnityEngine.Tooltip("Name of an empty GameObject in the target scene where players will spawn.")]
    public string targetSpawnPointName = "";

    [UnityEngine.Tooltip("If true, ALL connected players must be in the trigger zone to use the portal. If false, anyone can trigger.")]
    public bool requireAllPlayersInTrigger = true;

    private HashSet<ulong> playersInTrigger = new HashSet<ulong>();

    private NetworkVariable<bool> isArmed = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> readyCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsArmed => isArmed.Value;
    public int ReadyCount => readyCount.Value;
    public int TotalPlayers => NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList.Count : 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        var playerObj = other.GetComponentInParent<NetworkObject>();
        if (playerObj == null || !playerObj.IsPlayerObject) return;
        playersInTrigger.Add(playerObj.OwnerClientId);
        UpdateReadyState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) return;
        var playerObj = other.GetComponentInParent<NetworkObject>();
        if (playerObj == null || !playerObj.IsPlayerObject) return;
        playersInTrigger.Remove(playerObj.OwnerClientId);
        UpdateReadyState();
    }

    private void UpdateReadyState()
    {
        readyCount.Value = playersInTrigger.Count;
        isArmed.Value = !requireAllPlayersInTrigger || (playersInTrigger.Count >= TotalPlayers);
    }

    // Called by ReadyZone.onAllReady (and reused internally below).
    public void ServerInitiateTransition()
    {
        if (!IsServer) return;
        if (SceneFlowController.Instance == null)
        {
            Debug.LogError("[ScenePortal] SceneFlowController not found.");
            return;
        }
        if (string.IsNullOrEmpty(targetSceneName)) return;
        SceneFlowController.Instance.ServerTransitionToScene(targetSceneName, targetSpawnPointName);
    }
}