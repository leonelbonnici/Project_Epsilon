using Unity.Netcode;
using UnityEngine;

// Generic networked spawner. Place on any object (player, GameManager, spawn point).
// Only the server can spawn NetworkObjects, so client requests go through an RPC.
public class NetworkObjectSpawner : NetworkBehaviour
{
    [UnityEngine.Tooltip("Networked prefab to spawn. Must be in the NetworkManager's NetworkPrefabs list.")]
    public GameObject prefabToSpawn;

    [UnityEngine.Tooltip("If true, only the owner of THIS object may trigger a spawn (for player-attached spawners).")]
    public bool ownerOnly = true;

    // Call from PlayMaker: spawn at this object's position.
    public void SpawnHere() => SpawnAt(transform.position);

    // Call from PlayMaker: spawn at a specific position.
    public void SpawnAt(Vector3 position)
    {
        if (ownerOnly && !IsOwner) return;
        SpawnRpc(position);
    }

    [Rpc(SendTo.Server)]
    private void SpawnRpc(Vector3 position) => Spawn(position);

    // Server-side direct spawn (no RPC) — for server logic like a GameManager
    // spawning enemies on a timer. We'll use this in step 11's notes.
    public void ServerSpawnAt(Vector3 position)
    {
        if (IsServer) Spawn(position);
    }

    private void Spawn(Vector3 position)
    {
        if (prefabToSpawn == null) return;
        GameObject instance = Instantiate(prefabToSpawn, position, Quaternion.identity);
        instance.GetComponent<NetworkObject>().Spawn();
    }
}