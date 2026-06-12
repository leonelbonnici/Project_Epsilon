using Unity.Netcode;
using UnityEngine;

public class BossAttacks : NetworkBehaviour
{
    [UnityEngine.Tooltip("Slam radius around the boss.")]
    public float slamRadius = 3f;
    [UnityEngine.Tooltip("Slam damage to players caught in range.")]
    public float slamDamage = 20f;

    [UnityEngine.Tooltip("Boss projectile prefab (must be in the NetworkPrefabs list).")]
    public GameObject bossProjectilePrefab;
    [UnityEngine.Tooltip("Boss projectile speed.")]
    public float bossProjectileSpeed = 8f;
    [UnityEngine.Tooltip("Boss projectile damage.")]
    public float bossProjectileDamage = 15f;

    public void ServerSlam()
    {
        if (!IsServer) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius);
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && target.Team == Team.Player) target.ServerApplyDamage(slamDamage);
        }
    }

    public void ServerShoot()
    {
        if (!IsServer || bossProjectilePrefab == null) return;
        Transform target = GetNearestPlayer();
        if (target == null) return;

        Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
        Vector3 spawnPos = transform.position + (Vector3)(dir * 0.8f);
        GameObject obj = Instantiate(bossProjectilePrefab, spawnPos, Quaternion.identity);
        Projectile p = obj.GetComponent<Projectile>();
        if (p != null) p.Configure(dir, bossProjectileSpeed, bossProjectileDamage, Team.Player);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    private Transform GetNearestPlayer()
    {
        Transform nearest = null;
        float best = float.MaxValue;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject po = client.PlayerObject;
            if (po == null) continue;
            float d = ((Vector2)(po.transform.position - transform.position)).sqrMagnitude;
            if (d < best) { best = d; nearest = po.transform; }
        }
        return nearest;
    }

    // Tier 1 gizmo: see the slam radius in the Scene view when the boss is selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}