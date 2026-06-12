using Unity.Netcode;
using UnityEngine;
using HutongGames.PlayMaker;
using Unity.Collections;

// Networking substrate for a boss: server-authoritative health + phase, a damage
// entry point for player attacks, and PlayMaker events for presentation.
public class BossBridge : NetworkBehaviour, IDamageable
{
    public Team Team => Team.Enemy;  

    [UnityEngine.Tooltip("Starting / max health.")]
    public float maxHealth = 500f;

    [UnityEngine.Tooltip("Fired to this boss's FSMs when it spawns / is network-ready.")]
    public string SpawnEvent = "BOSS_SPAWNED";
    [UnityEngine.Tooltip("Fired whenever health changes.")]
    public string HealthChangedEvent = "BOSS_HEALTH_CHANGED";
    [UnityEngine.Tooltip("Fired whenever the phase changes.")]
    public string PhaseChangedEvent = "BOSS_PHASE_CHANGED";
    [UnityEngine.Tooltip("Fired (on all clients) when the boss dies.")]
    public string DiedEvent = "BOSS_DIED";

    private NetworkVariable<float> health = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> phase = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Reads for PlayMaker Get Property.
    public float HealthValue => health.Value;
    public float HealthNormalized => maxHealth > 0f ? health.Value / maxHealth : 0f;  // 0..1, handy for a health bar
    public int PhaseValue => phase.Value;
    public bool IsServerBrain => IsServer;   // the brain FSM (step 3) will gate on this

    // --- Damage ---
    // Player attacks call this. RequireOwnership = false because the boss is owned by
    // the SERVER — same lesson as the GameManager score: non-host clients must be allowed
    // to send it.
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestDamageRpc(float amount) => ApplyDamage(amount);

    // Server-side direct, for hits the server already evaluated (projectiles, melee).
    public void ServerApplyDamage(float amount) { if (IsServer) ApplyDamage(amount); }

    private void ApplyDamage(float amount)
    {
        if (health.Value <= 0f) return;                       // already dead
        health.Value = Mathf.Max(0f, health.Value - amount);
        CheckPhase(); 
        if (health.Value <= 0f) Die();
    }

    // --- Phase (server) --- (unused until we add multi-phase bosses; it's substrate)
    public void ServerSetPhase(int newPhase) { if (IsServer) phase.Value = newPhase; }

    private void Die()
    {
        DiedRpc();                                            // let every copy react first
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(); // then remove it everywhere
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DiedRpc() => SendEventToAllFsms(DiedEvent);

    // --- Lifecycle ---
    public override void OnNetworkSpawn()
    {
        if (IsServer) health.Value = maxHealth;               // server sets starting health
        health.OnValueChanged += HandleHealthChanged;
        phase.OnValueChanged += HandlePhaseChanged;
        SendEventToAllFsms(SpawnEvent);
        SendEventToAllFsms(HealthChangedEvent);               // show starting health
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= HandleHealthChanged;
        phase.OnValueChanged -= HandlePhaseChanged;
    }

    private void HandleHealthChanged(float prev, float curr) => SendEventToAllFsms(HealthChangedEvent);
    private void HandlePhaseChanged(int prev, int curr) => SendEventToAllFsms(PhaseChangedEvent);

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        PlayMakerFSM[] fsms = GetComponents<PlayMakerFSM>();
        foreach (PlayMakerFSM fsm in fsms) fsm.SendEvent(eventName);
    }

    // Server-side brain calls this to fire a PlayMaker event on every client's copy.
        public void ServerBroadcastEvent(string eventName)
    {
        if (IsServer) BroadcastEventRpc(eventName);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEventRpc(FixedString64Bytes eventName)
        => SendEventToAllFsms(eventName.ToString());
        
    [UnityEngine.Tooltip("Health fraction (0-1) at which the boss enters its second phase.")]
    public float phase2HealthFraction = 0.5f;

    // The brain reads this each cycle — phase 2 is faster.
    public float AttackCooldown => phase.Value >= 1 ? 1.0f : 2.0f;

    private void CheckPhase()
    {
        if (phase.Value == 0 && health.Value <= maxHealth * phase2HealthFraction)
            phase.Value = 1;   // server-write -> syncs + fires BOSS_PHASE_CHANGED on every client
    }           
}