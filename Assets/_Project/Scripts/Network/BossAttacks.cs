using Unity.Netcode;
using UnityEngine;

// Server-side execution of the boss's attacks. The (server-gated) brain FSM calls these.
public class BossAttacks : NetworkBehaviour
{
    [UnityEngine.Tooltip("Slam radius around the boss.")]
    public float slamRadius = 3f;
    [UnityEngine.Tooltip("Slam damage to players caught in range.")]
    public float slamDamage = 20f;

    // Telegraphed ground-slam: hits players within slamRadius at the moment it lands.
    public void ServerSlam()
    {
        if (!IsServer) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius);
        foreach (Collider2D hit in hits)
        {
            NetworkPlayMakerBridge player = hit.GetComponentInParent<NetworkPlayMakerBridge>();
            if (player != null) player.ServerApplyDamage(slamDamage);
        }
    }
}