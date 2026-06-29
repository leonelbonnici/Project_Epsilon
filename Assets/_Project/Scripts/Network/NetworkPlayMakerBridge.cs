using Unity.Netcode;
using UnityEngine;
using HutongGames.PlayMaker;

public class NetworkPlayMakerBridge : NetworkBehaviour, IDamageable
{
    [UnityEngine.Tooltip("Starting / max health.")]
    public float maxHealth = 100f;

    // Fires on every client when health changes. Signature: (previousValue, currentValue).
    public event System.Action<float, float> HealthChanged;

    // Fires on every client when downed state flips. Signature: (isNowDowned).
    public event System.Action<bool> DownedChanged;

    public Team Team => Team.Player;

    [UnityEngine.Tooltip("Broadcast when this object spawns.")]
    public string SpawnEvent = "NETWORK_SPAWNED";
    [UnityEngine.Tooltip("Broadcast when this object despawns.")]
    public string DespawnEvent = "NETWORK_DESPAWNED";
    [UnityEngine.Tooltip("Broadcast when health changes.")]
    public string HealthChangedEvent = "HEALTH_CHANGED";
    [UnityEngine.Tooltip("Broadcast (on all clients) when this object is hit.")]
    public string HitEffectEvent = "HIT_EFFECT";
    [UnityEngine.Tooltip("Broadcast (on all clients) when this player enters the downed state.")]
    public string DownedEvent = "PLAYER_DOWNED";
    [UnityEngine.Tooltip("Broadcast (on all clients) when this player is revived from downed.")]
    public string RevivedEvent = "PLAYER_REVIVED";

    // SERVER-write: only the server changes health/state. Clients request via RPC.
    private NetworkVariable<float> health = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField]
    private NetworkVariable<bool> isDowned = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsLocalOwner => IsOwner;
    public int OwnerId => (int)OwnerClientId;
    public float HealthValue => health.Value;
    public float HealthNormalized => maxHealth > 0f ? health.Value / maxHealth : 0f;
    public bool IsDowned => isDowned.Value;

    // --- Lifecycle ---
    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += HandleHealthChanged;
        isDowned.OnValueChanged += HandleDownedChanged;
        SendEventToAllFsms(SpawnEvent);
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= HandleHealthChanged;
        isDowned.OnValueChanged -= HandleDownedChanged;
        SendEventToAllFsms(DespawnEvent);
    }

    private void HandleHealthChanged(float previousValue, float newValue)
    {
        SendEventToAllFsms(HealthChangedEvent);
        HealthChanged?.Invoke(previousValue, newValue);
    }

    private void HandleDownedChanged(bool prev, bool curr)
    {
        SendEventToAllFsms(curr ? DownedEvent : RevivedEvent);
        DownedChanged?.Invoke(curr);
    }

    // --- PlayMaker entry points (called on the owning client) ---
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

    // --- Client -> server RPCs ---
    [Rpc(SendTo.Server)]
    private void TakeDamageRpc(float amount) => ApplyDamage(amount);

    [Rpc(SendTo.Server)]
    private void HealRpc(float amount)
    {
        if (isDowned.Value) return;
        health.Value = Mathf.Min(maxHealth, health.Value + amount);
    }

    // --- Server -> all clients ---
    [Rpc(SendTo.ClientsAndHost)]
    private void HitEffectRpc() => SendEventToAllFsms(HitEffectEvent);

    // --- Server-side public API ---
    public void ServerApplyDamage(float amount)
    {
        if (!IsServer) return;
        ApplyDamage(amount);
    }

    public void ServerHealFull()
    {
        if (!IsServer) return;
        if (isDowned.Value) return; // need revive, not heal
        health.Value = maxHealth;
    }

    public void ServerHeal(float amount)
    {
        if (!IsServer) return;
        if (isDowned.Value) return;
        health.Value = Mathf.Min(maxHealth, health.Value + amount);
    }

    /// <summary>
    /// Server-only: revives a downed player and restores them to the given health amount (default = full).
    /// </summary>
    public void ServerRevive(float toHealth = -1f)
    {
        if (!IsServer) return;
        if (!isDowned.Value) return;

        isDowned.Value = false;
        health.Value = toHealth < 0f ? maxHealth : Mathf.Clamp(toHealth, 0f, maxHealth);
    }

    public void ServerApplyImpulse(Vector2 direction, float distance, float duration)
    {
        if (!IsServer) return;
        ApplyImpulseRpc(direction, distance, duration);
    }

    public void ServerTeleportPlayer(Vector3 position)
    {
        if (!IsServer) return;
        TeleportRpc(position);
    }

    // --- Server-side helpers ---
    private void ApplyDamage(float amount)
    {
        if (isDowned.Value) return; // damage immunity while downed
        if (health.Value <= 0f) return;

        health.Value = Mathf.Max(0f, health.Value - amount);
        HitEffectRpc();

        if (health.Value <= 0f)
        {
            isDowned.Value = true;
        }
    }

    // --- Impulse routine (server -> owner) ---
    [Rpc(SendTo.Owner)]
    private void ApplyImpulseRpc(Vector2 direction, float distance, float duration)
    {
        StartCoroutine(ImpulseRoutine(direction, distance, duration));
    }

    private System.Collections.IEnumerator ImpulseRoutine(Vector2 direction, float distance, float duration)
    {
        if (duration <= 0f) yield break;
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(direction.normalized * distance);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
        transform.position = end;
    }

    // --- Teleport routine (server -> owner) ---
    [Rpc(SendTo.Owner)]
    private void TeleportRpc(Vector3 position)
    {
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            nt.Teleport(position, transform.rotation, transform.localScale);
        }
        else
        {
            transform.position = position;
        }

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // --- Utility ---
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