using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Per-area orchestrator. Tracks rooms and doors, re-evaluates door locks when rooms complete.
// Fires AREA_CLEARED when all "required for completion" rooms are done.
public class AreaBridge : NetworkBehaviour
{
    [UnityEngine.Tooltip("Room IDs required to complete this area. When all are cleared, fires AREA_CLEARED.")]
    public List<string> requiredRoomIds = new List<string>();

    [UnityEngine.Tooltip("Fired when the area is network-ready.")]
    public string SpawnEvent = "AREA_SPAWNED";
    [UnityEngine.Tooltip("Fired when this area's required rooms are all complete.")]
    public string ClearedEvent = "AREA_CLEARED";

    private Dictionary<string, IRoom> rooms = new Dictionary<string, IRoom>();
    private List<Door> doors = new List<Door>();
    private bool areaCleared = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer) RegisterRoomsAndDoors();
        SendEventToAllFsms(SpawnEvent);
    }

    private void RegisterRoomsAndDoors()
    {
        // Inventory rooms — every IRoom in the scene at spawn time.
        foreach (var room in FindAllRooms())
        {
            if (rooms.ContainsKey(room.RoomId))
            {
                Debug.LogWarning($"[AreaBridge] Duplicate room id: {room.RoomId}");
                continue;
            }
            rooms.Add(room.RoomId, room);
            room.RoomCompleted += OnRoomCompleted;
        }

        // Inventory doors and evaluate their initial state in case any have no prerequisites.
        doors.AddRange(FindObjectsByType<Door>(FindObjectsSortMode.None));
        foreach (var door in doors) door.ServerEvaluate(rooms);
    }

    private static IEnumerable<IRoom> FindAllRooms()
    {
        foreach (var mono in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mono is IRoom room) yield return room;
    }

    private void OnRoomCompleted(IRoom room)
    {
        if (!IsServer) return;

        // Re-evaluate every door's unlock condition.
        foreach (var door in doors) door.ServerEvaluate(rooms);

        // Check if the area itself is now complete.
        if (!areaCleared && AllRequiredRoomsCleared())
        {
            areaCleared = true;
            BroadcastEventRpc(ClearedEvent);
        }
    }

    private bool AllRequiredRoomsCleared()
    {
        foreach (string id in requiredRoomIds)
            if (!rooms.TryGetValue(id, out IRoom r) || !r.IsCompleted) return false;
        return true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEventRpc(Unity.Collections.FixedString64Bytes e) => SendEventToAllFsms(e.ToString());

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }
}