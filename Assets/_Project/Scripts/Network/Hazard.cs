using Unity.Netcode;
using UnityEngine;

// Server-driven AoE hazard. Two modes via `lingerDuration`:
//   = 0  → single-fire hazard (telegraph, damage once, despawn). Targeted-hazard mode.
//   > 0  → lingering hazard (telegraph, then tick damage for lingerDuration, despawn).
[RequireComponent(typeof(NetworkObject))]
public class Hazard : NetworkBehaviour
{
    [UnityEngine.Tooltip("How long the warning telegraph lasts before damage begins.")]
    public float telegraphDuration = 1.2f;
    [UnityEngine.Tooltip("How long the hazard stays active dealing tick damage. 0 = single-fire (targeted-hazard mode).")]
    public float lingerDuration = 0f;
    [UnityEngine.Tooltip("Tick interval in seconds while lingering. Ignored if lingerDuration = 0.")]
    public float tickInterval = 0.5f;
    [UnityEngine.Tooltip("How long the hazard stays visible AFTER its active phase (lets the visual clear).")]
    public float postFireLinger = 0.1f;
    [UnityEngine.Tooltip("Damage radius.")]
    public float radius = 2.5f;
    [UnityEngine.Tooltip("Damage dealt per hit (per tick if lingering).")]
    public float damage = 30f;

    [UnityEngine.Tooltip("Team this hazard hurts.")]
    public Team targetTeam = Team.Player;

    [UnityEngine.Tooltip("PlayMaker event fired on all clients when the telegraph starts.")]
    public string TelegraphEvent = "HAZARD_TELEGRAPH";
    [UnityEngine.Tooltip("PlayMaker event fired on all clients when the hazard goes active (first damage).")]
    public string FireEvent = "HAZARD_FIRE";

    public float TelegraphDuration => telegraphDuration;

    public override void OnNetworkSpawn()
    {
        SendEventToAllFsms(TelegraphEvent);
        if (IsServer) StartCoroutine(HazardRoutine());
    }

    private System.Collections.IEnumerator HazardRoutine()
    {
        yield return new WaitForSeconds(telegraphDuration);

        // First damage moment + visual cue for "active now".
        FireRpc();
        ApplyTick();

        // Linger mode: keep ticking until lingerDuration elapses.
        if (lingerDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < lingerDuration)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                ApplyTick();
            }
        }

        yield return new WaitForSeconds(postFireLinger);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    private void ApplyTick()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && target.Team == targetTeam) target.ServerApplyDamage(damage);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FireRpc() => SendEventToAllFsms(FireEvent);

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }
}