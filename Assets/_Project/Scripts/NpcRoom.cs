using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// IRoom that completes when each required NPC has been talked to.
// Same shape as PickupRoom and PuzzleRoom — different interactable type, same pattern.
public class NpcRoom : NetworkBehaviour, IRoom
{
    [UnityEngine.Tooltip("Unique identifier within the area.")]
    public string roomId = "npc_room_01";

    [UnityEngine.Tooltip("NPC IDs that must be talked to in order for this room to clear.")]
    public List<string> requiredNpcIds = new List<string>();

    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private List<NpcInteractable> trackedNpcs = new List<NpcInteractable>();

    public string RoomId => roomId;
    public bool IsCompleted => isCompleted.Value;
    public event System.Action<IRoom> RoomCompleted;

    public override void OnNetworkSpawn()
    {
        if (IsServer) SubscribeToSceneNpcs();
    }

    public override void OnNetworkDespawn()
    {
        foreach (var npc in trackedNpcs)
            if (npc != null) npc.ConversationEnded -= OnNpcConversationEnded;
        trackedNpcs.Clear();
    }

    private void SubscribeToSceneNpcs()
    {
        foreach (var npc in FindObjectsByType<NpcInteractable>(FindObjectsSortMode.None))
        {
            if (requiredNpcIds.Contains(npc.NpcId))
            {
                npc.ConversationEnded += OnNpcConversationEnded;
                trackedNpcs.Add(npc);
            }
        }
    }

    private void OnNpcConversationEnded(NpcInteractable npc)
    {
        if (!IsServer) return;
        if (isCompleted.Value) return;

        // Room is done when every required NPC has been talked to.
        foreach (string requiredId in requiredNpcIds)
        {
            NpcInteractable found = trackedNpcs.Find(n => n.NpcId == requiredId);
            if (found == null || !found.IsCompleted) return;       // either missing or not yet talked to
        }

        isCompleted.Value = true;
        RoomCompleted?.Invoke(this);
    }
}