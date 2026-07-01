using Unity.Netcode;
using UnityEngine;
using HutongGames.PlayMaker;
using Unity.Collections;
using System.Collections.Generic;

// Networking substrate for a boss: server-authoritative health + phase, a damage
// entry point for player attacks, and PlayMaker events for presentation.
public class BossBridge : NetworkBehaviour, IDamageable
{
    [UnityEngine.Tooltip("Name shown on the boss health bar (e.g., 'The Slammer'). Falls back to GameObject name if empty.")]
    public string displayName = "";

    // Fires on every client when boss health changes. Signature: (previousValue, currentValue).
    public event System.Action<float, float> HealthChanged;

    // Server-side hook: fires when this boss dies. Used by the arena to detect a clear.
    public event System.Action DiedRaised;

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
    // Synced effective max health (after player-count scaling). Server writes once on spawn.
    private NetworkVariable<float> effectiveMaxHealth = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public float MaxHealthValue => effectiveMaxHealth.Value > 0f ? effectiveMaxHealth.Value : maxHealth;

    // Reads for PlayMaker Get Property.
    public float HealthValue => health.Value;
    public float HealthNormalized => MaxHealthValue > 0f ? health.Value / MaxHealthValue : 0f;
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
        if (IsServer) DiedRaised?.Invoke();   // <-- add this line first
        DiedRpc();
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DiedRpc() => SendEventToAllFsms(DiedEvent);

    // --- Lifecycle ---
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            effectiveMaxHealth.Value = maxHealth;
            health.Value = maxHealth;
        }
        health.OnValueChanged += HandleHealthChanged;
        phase.OnValueChanged += HandlePhaseChanged;
        SendEventToAllFsms(SpawnEvent);
        SendEventToAllFsms(HealthChangedEvent);
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= HandleHealthChanged;
        phase.OnValueChanged -= HandlePhaseChanged;
    }

    private void HandleHealthChanged(float prev, float curr)
    {
        SendEventToAllFsms(HealthChangedEvent);
        HealthChanged?.Invoke(prev, curr);
    }

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
        
    [System.Serializable]
    public class BossPhase
    {
        [UnityEngine.Tooltip("HP fraction at which the boss enters this phase. Phase 0 (first list entry) is entered on spawn — its value is ignored.")]
        [Range(0f, 1f)]
        public float enterAtHpFraction = 1f;

        [UnityEngine.Tooltip("Delay between attacks while in this phase. Lower = more aggressive.")]
        public float attackCooldown = 2.0f;
    }

    [UnityEngine.Tooltip("Boss phases in order. List index = phase number. Entry 0 is the starting phase; subsequent entries define HP thresholds for auto-transitions. HP fractions should decrease down the list (e.g. 1, 0.66, 0.33 for 3 phases).")]
    public List<BossPhase> phases = new List<BossPhase>() { new BossPhase() };

    public float AttackCooldown
    {
        get
        {
            if (phases == null || phases.Count == 0) return 2f;
            int p = Mathf.Clamp(phase.Value, 0, phases.Count - 1);
            return phases[p].attackCooldown;
        }
    }

    private void CheckPhase()
    {
        if (!IsServer || phases == null || phases.Count == 0) return;

        float hpFrac = MaxHealthValue > 0f ? health.Value / MaxHealthValue : 0f;

        // Walk forward from the current phase. Advance as many phases as HP has crossed
        // (handles massive single-hit damage that skips a phase).
        int newPhase = phase.Value;
        for (int next = newPhase + 1; next < phases.Count; next++)
        {
            if (hpFrac <= phases[next].enterAtHpFraction) newPhase = next;
            else break;
        }
        if (newPhase != phase.Value) phase.Value = newPhase;
    }         
}