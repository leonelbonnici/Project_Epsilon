using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EndAreaAltar : NetworkBehaviour, IInteractable, IPersistable
{
    [UnityEngine.Tooltip("Unique identifier for this altar within its area.")]
    public string altarId = "altar_01";

    [UnityEngine.Tooltip("Cutscene playback duration in seconds before teleporting players to the hub.")]
    public float cutsceneDuration = 3f;

    [UnityEngine.Tooltip("Scene to load when cutscene completes.")]
    public string targetHubScene = "Hub";
    [UnityEngine.Tooltip("Spawn point name in the target hub scene.")]
    public string targetHubSpawn = "PlayerSpawn_HubArrival";

    [UnityEngine.Tooltip("Visual shown when altar is available to interact with (after the boss is defeated).")]
    public GameObject activeVisual;
    [UnityEngine.Tooltip("Visual shown after the altar has been used (cleared area on re-entry).")]
    public GameObject usedVisual;

    [UnityEngine.Tooltip("FSM event broadcast to all clients when the cutscene starts.")]
    public string CutsceneStartEvent = "CUTSCENE_START";
    [UnityEngine.Tooltip("FSM event broadcast to all clients when the cutscene ends (just before teleport).")]
    public string CutsceneDoneEvent = "CUTSCENE_DONE";
    [UnityEngine.Tooltip("FSM event broadcast when the altar drops (boss death). Useful for spawn FX/sound.")]
    public string DroppedEvent = "ALTAR_DROPPED";

    private NetworkVariable<bool> hasBeenUsed = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> hasDropped = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> altarPosition = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsAvailable => hasDropped.Value && !hasBeenUsed.Value;

    public override void OnNetworkSpawn()
    {
        hasBeenUsed.OnValueChanged += OnUsedChanged;
        hasDropped.OnValueChanged += OnDroppedChanged;
        altarPosition.OnValueChanged += OnPositionChanged;

        // If we spawned with a previously-restored position, apply it
        if (hasDropped.Value)
        {
            transform.position = altarPosition.Value;
        }

        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        hasBeenUsed.OnValueChanged -= OnUsedChanged;
        hasDropped.OnValueChanged -= OnDroppedChanged;
        altarPosition.OnValueChanged -= OnPositionChanged;
    }

    private void OnUsedChanged(bool prev, bool curr) => UpdateVisuals();
    private void OnDroppedChanged(bool prev, bool curr) => UpdateVisuals();
    private void OnPositionChanged(Vector3 prev, Vector3 curr) => transform.position = curr;

    /// <summary>
    /// Server-only: called by the boss arena when its boss dies, to drop the altar at the boss's death position.
    /// </summary>
    public void ServerDropAtPosition(Vector3 position)
    {
        if (!IsServer) return;
        if (hasDropped.Value) return; // Already dropped (e.g., on restore); don't overwrite

        altarPosition.Value = position;
        transform.position = position;
        hasDropped.Value = true;

        BroadcastEventRpc(DroppedEvent);
    }

    private void UpdateVisuals()
    {
        bool used = hasBeenUsed.Value;
        bool available = IsAvailable;

        // Hidden and non-blocking until dropped. Once dropped, shows either active or used visual.
        if (activeVisual != null) activeVisual.SetActive(available);
        if (usedVisual != null) usedVisual.SetActive(used);

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = available || used;
        }
    }

    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        if (!IsAvailable) return;

        hasBeenUsed.Value = true;
        StartCoroutine(PlayCutsceneAndTeleport());
    }

    private IEnumerator PlayCutsceneAndTeleport()
    {
        BroadcastEventRpc(CutsceneStartEvent);
        yield return new WaitForSeconds(cutsceneDuration);
        BroadcastEventRpc(CutsceneDoneEvent);

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.ServerTransitionToScene(targetHubScene, targetHubSpawn);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEventRpc(Unity.Collections.FixedString64Bytes e)
        => SendEventToAllFsms(e.ToString());

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }

    // --- IPersistable ---
    [System.Serializable]
    private struct PersistedState
    {
        public bool dropped;
        public bool used;
        public Vector3 position;
    }

    public string PersistenceId => $"altar:{altarId}";

    public string CaptureState()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            dropped = hasDropped.Value,
            used = hasBeenUsed.Value,
            position = altarPosition.Value
        });
    }

    public void RestoreState(string state)
    {
        var data = JsonUtility.FromJson<PersistedState>(state);
        hasDropped.Value = data.dropped;
        hasBeenUsed.Value = data.used;
        altarPosition.Value = data.position;
        if (data.dropped)
        {
            transform.position = data.position;
        }
    }
}