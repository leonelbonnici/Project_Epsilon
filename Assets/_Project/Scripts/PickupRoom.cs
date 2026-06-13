using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// IRoom that completes when all required pickup IDs in this room are collected.
// Configure with one or many — the logic is the same.
public class PickupRoom : NetworkBehaviour, IRoom
{
    [UnityEngine.Tooltip("Unique identifier within the area.")]
    public string roomId = "pickup_room_01";

    [UnityEngine.Tooltip("Pickup IDs required to clear this room. The room watches for each to be collected.")]
    public List<string> requiredPickupIds = new List<string>();

    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private HashSet<string> collectedIds = new HashSet<string>();
    private List<PickupCollectible> subscribedPickups = new List<PickupCollectible>();

    public string RoomId => roomId;
    public bool IsCompleted => isCompleted.Value;
    public event System.Action<IRoom> RoomCompleted;

    public override void OnNetworkSpawn()
    {
        if (IsServer) SubscribeToScenePickups();
    }

    public override void OnNetworkDespawn()
    {
        foreach (var pickup in subscribedPickups)
            if (pickup != null) pickup.CollectedRaised -= OnPickupCollected;
        subscribedPickups.Clear();
    }

    private void SubscribeToScenePickups()
    {
        // Find every pickup currently in the scene whose ID is in our required list.
        // Pickups must exist at room spawn time (in-scene placed, like the room itself).
        foreach (var pickup in FindObjectsByType<PickupCollectible>(FindObjectsSortMode.None))
        {
            if (requiredPickupIds.Contains(pickup.PickupId))
            {
                pickup.CollectedRaised += OnPickupCollected;
                subscribedPickups.Add(pickup);
            }
        }
    }

    private void OnPickupCollected(PickupCollectible pickup)
    {
        if (!IsServer) return;
        if (isCompleted.Value) return;

        collectedIds.Add(pickup.PickupId);

        // Room is done when every required ID has been collected.
        if (collectedIds.IsSupersetOf(requiredPickupIds))
        {
            isCompleted.Value = true;
            RoomCompleted?.Invoke(this);
        }
    }
}