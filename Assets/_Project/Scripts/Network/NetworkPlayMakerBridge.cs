using Unity.Netcode;
using UnityEngine;
using HutongGames.PlayMaker;

public class NetworkPlayMakerBridge : NetworkBehaviour
{
    [UnityEngine.Tooltip("Broadcast when this object spawns.")]
    public string SpawnEvent = "NETWORK_SPAWNED";
    [UnityEngine.Tooltip("Broadcast when this object despawns.")]
    public string DespawnEvent = "NETWORK_DESPAWNED";
    [UnityEngine.Tooltip("Broadcast when health changes.")]
    public string HealthChangedEvent = "HEALTH_CHANGED";
    [UnityEngine.Tooltip("Broadcast (on all clients) when this object is hit.")]
    public string HitEffectEvent = "HIT_EFFECT";

    // SERVER-write now: only the server changes health. Clients request via RPC.
    private NetworkVariable<float> health = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsLocalOwner => IsOwner;
    public int OwnerId => (int)OwnerClientId;
    public float HealthValue => health.Value;

    // --- PlayMaker entry points (called on the owning client) ---
    // The IsOwner guard ensures only the owned copy sends the request,
    // so one key press doesn't damage every player's copy in the scene.
    public void RequestDamage(float amount)
    {
        if (!IsOwner) return;
        TakeDamageRpc(amount);
    }

    public void RequestHeal(float amount)
    {
        if (!IsOwner) return;
        HealRpc(amount);
    }

    // --- Runs on the SERVER (client -> server) ---
    [Rpc(SendTo.Server)]
    private void TakeDamageRpc(float amount)
    {
        // The server is the authority. A real game would validate here
        // (is the attacker in range? is the target alive? etc.)
        health.Value = Mathf.Max(0f, health.Value - amount);
        HitEffectRpc(); // tell everyone to play a hit reaction
    }

    [Rpc(SendTo.Server)]
    private void HealRpc(float amount)
    {
        health.Value = Mathf.Min(100f, health.Value + amount);
    }

    // --- Runs on ALL clients + host (server -> clients) ---
    [Rpc(SendTo.ClientsAndHost)]
    private void HitEffectRpc()
    {
        SendEventToAllFsms(HitEffectEvent);
    }

    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += HandleHealthChanged;
        SendEventToAllFsms(SpawnEvent);
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= HandleHealthChanged;
        SendEventToAllFsms(DespawnEvent);
    }

    private void HandleHealthChanged(float previousValue, float newValue)
    {
        SendEventToAllFsms(HealthChangedEvent);
    }

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        var fsms = GetComponents<PlayMakerFSM>();
        foreach (var fsm in fsms)
        {
            fsm.SendEvent(eventName);
        }
    }
}