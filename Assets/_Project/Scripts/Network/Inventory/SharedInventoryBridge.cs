using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Party-wide inventory. Server-authoritative. All party members share one bag.
// Boss keys, quest items, consumables all live here.
public struct InventoryEntry : INetworkSerializable, System.IEquatable<InventoryEntry>
{
    public FixedString32Bytes itemId;
    public int count;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref itemId);
        s.SerializeValue(ref count);
    }

    public bool Equals(InventoryEntry other) => itemId.Equals(other.itemId);
}

public class SharedInventoryBridge : NetworkBehaviour, IPersistable
{
    public static SharedInventoryBridge Instance { get; private set; }

    public NetworkList<InventoryEntry> Entries;

    public string PersistenceId => "shared_inventory";

    private void Awake()
    {
        Entries = new NetworkList<InventoryEntry>();
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Server-side mutations ---

    public void ServerAddItem(string itemId, int count = 1)
    {
        if (!IsServer) return;
        if (string.IsNullOrEmpty(itemId) || count == 0) return;

        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].itemId.ToString() == itemId)
            {
                var e = Entries[i];
                e.count += count;
                Entries.RemoveAt(i);
                if (e.count > 0) Entries.Insert(i, e);
                return;
            }
        }

        if (count > 0)
        {
            Entries.Add(new InventoryEntry { itemId = itemId, count = count });
        }
    }

    public bool ServerRemoveItem(string itemId, int count = 1)
    {
        if (!IsServer) return false;
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].itemId.ToString() != itemId) continue;
            if (Entries[i].count < count) return false;

            var e = Entries[i];
            e.count -= count;
            Entries.RemoveAt(i);
            if (e.count > 0) Entries.Insert(i, e);
            return true;
        }
        return false;
    }

    // --- Client-safe reads ---

    public int GetCount(string itemId)
    {
        if (Entries == null) return 0;
        foreach (var e in Entries)
            if (e.itemId.ToString() == itemId) return e.count;
        return 0;
    }

    public bool HasItem(string itemId, int count = 1) => GetCount(itemId) >= count;

    // --- IPersistable ---

    public string CaptureState()
    {
        if (Entries == null || Entries.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var e in Entries)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(e.itemId).Append(':').Append(e.count);
        }
        return sb.ToString();
    }

    public void RestoreState(string state)
    {
        if (!IsServer) return;
        Entries.Clear();
        if (string.IsNullOrEmpty(state)) return;

        foreach (var pair in state.Split('|'))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2) continue;
            if (int.TryParse(parts[1], out int count) && count > 0)
            {
                Entries.Add(new InventoryEntry { itemId = parts[0], count = count });
            }
        }
    }
}