using Unity.Netcode;
using UnityEngine;

// Minimal IRoom: completes the moment any player enters the trigger.
// Use as a placeholder for filler rooms, lore stops, narrative beats — anything
// that just needs the player to "be there" to count as cleared.
[RequireComponent(typeof(NetworkObject))]
public class WalkthroughRoom : NetworkBehaviour, IRoom
{
    [UnityEngine.Tooltip("Unique identifier within the area.")]
    public string roomId = "walkthrough_01";

    [UnityEngine.Tooltip("If true, this room can only be completed once. If false, it re-fires every entry (uncommon, but available).")]
    public bool onceOnly = true;

    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string RoomId => roomId;
    public bool IsCompleted => isCompleted.Value;
    public event System.Action<IRoom> RoomCompleted;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (onceOnly && isCompleted.Value) return;
        if (other.GetComponentInParent<NetworkPlayMakerBridge>() == null) return;  // only players

        isCompleted.Value = true;
        RoomCompleted?.Invoke(this);
    }
}