using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative door. Locked until its prerequisite rooms are all (or any) completed.
// Server controls a NetworkVariable<bool> for locked state; clients react to it via OnValueChanged.
public class Door : NetworkBehaviour
{
    [UnityEngine.Tooltip("Room IDs that must be completed for this door to unlock.")]
    public List<string> requiredRoomIds = new List<string>();

    [UnityEngine.Tooltip("If true, ALL required rooms must be cleared. If false, ANY one of them.")]
    public bool requireAll = true;

    [UnityEngine.Tooltip("Visual when locked (active when locked, inactive when unlocked).")]
    public GameObject lockedVisual;

    [UnityEngine.Tooltip("Collider that blocks the path when locked.")]
    public Collider2D blockerCollider;

    [UnityEngine.Tooltip("Should this door START unlocked? Useful for entry doors.")]
    public bool startUnlocked = false;

    private NetworkVariable<bool> isLocked = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsLocked => isLocked.Value;
    public IReadOnlyList<string> RequiredRoomIds => requiredRoomIds;

    public override void OnNetworkSpawn()
    {
        isLocked.OnValueChanged += HandleLockChanged;
        if (IsServer) isLocked.Value = !startUnlocked;
        ApplyLockState(isLocked.Value);
    }

    public override void OnNetworkDespawn()
    {
        isLocked.OnValueChanged -= HandleLockChanged;
    }

    // Called by AreaFlow on the server when room state changes.
    public void ServerEvaluate(IReadOnlyDictionary<string, IRoom> rooms)
    {
        if (!IsServer) return;
        if (!isLocked.Value) return;       // already unlocked, nothing to do

        bool shouldUnlock = EvaluateUnlockCondition(rooms);
        if (shouldUnlock) isLocked.Value = false;
    }

    private bool EvaluateUnlockCondition(IReadOnlyDictionary<string, IRoom> rooms)
    {
        if (requiredRoomIds.Count == 0) return true;     // no prerequisites = always unlocked

        if (requireAll)
        {
            foreach (string id in requiredRoomIds)
                if (!rooms.TryGetValue(id, out IRoom r) || !r.IsCompleted) return false;
            return true;
        }
        else
        {
            foreach (string id in requiredRoomIds)
                if (rooms.TryGetValue(id, out IRoom r) && r.IsCompleted) return true;
            return false;
        }
    }

    private void HandleLockChanged(bool prev, bool curr) => ApplyLockState(curr);

    private void ApplyLockState(bool locked)
    {
        if (lockedVisual != null) lockedVisual.SetActive(locked);
        if (blockerCollider != null) blockerCollider.enabled = locked;
    }
}