using Unity.Netcode;
using UnityEngine;

// Server-driven AoE hazard: telegraphs at a position, then deals damage in radius, then despawns.
[RequireComponent(typeof(NetworkObject))]
public class Hazard : NetworkBehaviour
{
    [UnityEngine.Tooltip("How long the warning telegraph lasts before the hazard fires.")]
    public float telegraphDuration = 1.2f;
    [UnityEngine.Tooltip("How long the hazard lingers AFTER firing (lets the visual clear).")]
    public float postFireLinger = 0.1f;
    [UnityEngine.Tooltip("Damage radius.")]
    public float radius = 2.5f;
    [UnityEngine.Tooltip("Damage dealt to each player in range.")]
    public float damage = 30f;

    [UnityEngine.Tooltip("Team this hazard hurts (typically Player when spawned by boss).")]
    public Team targetTeam = Team.Player;

    [UnityEngine.Tooltip("PlayMaker event fired on all clients when the telegraph starts.")]
    public string TelegraphEvent = "HAZARD_TELEGRAPH";
    [UnityEngine.Tooltip("PlayMaker event fired on all clients when the hazard fires (damage moment).")]
    public string FireEvent = "HAZARD_FIRE";

    public float TelegraphDuration => telegraphDuration;   // Get Property for visuals

    public override void OnNetworkSpawn()
    {
        SendEventToAllFsms(TelegraphEvent);                // visuals start now (every client)
        if (IsServer) StartCoroutine(HazardRoutine());
    }

    private System.Collections.IEnumerator HazardRoutine()
    {
        yield return new WaitForSeconds(telegraphDuration);

        // Fire — overlap query, damage anyone of the target team in radius.
        FireRpc();
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && target.Team == targetTeam) target.ServerApplyDamage(damage);
        }

        yield return new WaitForSeconds(postFireLinger);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FireRpc() => SendEventToAllFsms(FireEvent);

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }
}