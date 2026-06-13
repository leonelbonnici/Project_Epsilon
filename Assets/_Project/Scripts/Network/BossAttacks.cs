using Unity.Netcode;
using UnityEngine;

public class BossAttacks : NetworkBehaviour
{
    [UnityEngine.Tooltip("Pull/push: how far each player is moved.")]
    public float pullPushDistance = 4f;
    [UnityEngine.Tooltip("Pull/push: how long the movement takes.")]
    public float pullPushDuration = 0.35f;

    [UnityEngine.Tooltip("Lingering hazard prefab to spawn for the area-denial attack.")]
    public GameObject lingeringHazardPrefab;

    [UnityEngine.Tooltip("Bullet ring: number of projectiles fired in the radial volley.")]
    public int ringCount = 12;
    [UnityEngine.Tooltip("Bullet ring: starting rotation offset (degrees), useful for asymmetric variants.")]
    public float ringStartAngle = 0f;

    [UnityEngine.Tooltip("Hazard prefab to spawn for the targeted-hazard attack.")]
    public GameObject hazardPrefab;

    [UnityEngine.Tooltip("Spread shot: number of projectiles per volley.")]
    public int spreadCount = 3;
    [UnityEngine.Tooltip("Spread shot: total fan angle in degrees (split evenly across the count).")]
    public float spreadAngle = 30f;

    [UnityEngine.Tooltip("Dash distance (units). Boss travels this far toward the target.")]
    public float dashDistance = 6f;
    [UnityEngine.Tooltip("Dash duration in seconds.")]
    public float dashDuration = 0.4f;
    [UnityEngine.Tooltip("Dash damage to players caught in the path.")]
    public float dashDamage = 25f;
    [UnityEngine.Tooltip("Dash hit radius (how wide the boss's collision is during dash).")]
    public float dashRadius = 1.2f;

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
        SpawnBossProjectile(dir);
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

    // Telegraphed lunge toward the nearest player. Damages anyone the boss passes through.
    public void ServerDash()
    {
        if (!IsServer) return;
        Transform target = GetNearestPlayer();
        if (target == null) return;

        Vector2 direction = ((Vector2)(target.position - transform.position)).normalized;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (Vector3)(direction * dashDistance);

        StartCoroutine(DashRoutine(startPos, endPos));
    }

    private System.Collections.IEnumerator DashRoutine(Vector3 startPos, Vector3 endPos)
    {
        float elapsed = 0f;
        var hitPlayers = new System.Collections.Generic.HashSet<IDamageable>();

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);

            // Overlap check at the boss's current position — damage each player at most once per dash.
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, dashRadius);
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target != null && target.Team == Team.Player && !hitPlayers.Contains(target))
                {
                    target.ServerApplyDamage(dashDamage);
                    hitPlayers.Add(target);
                }
            }

            yield return null;
        }

        transform.position = endPos;
    }

    public void ServerSpreadShot()
    {
        if (!IsServer || bossProjectilePrefab == null) return;
        Transform target = GetNearestPlayer();
        if (target == null) return;

        Vector2 baseDir = ((Vector2)(target.position - transform.position)).normalized;
        float halfSpread = spreadAngle * 0.5f;
        float step = spreadCount > 1 ? spreadAngle / (spreadCount - 1) : 0f;

        for (int i = 0; i < spreadCount; i++)
        {
            float offsetDeg = -halfSpread + step * i;
            Vector2 dir = Rotate(baseDir, offsetDeg);
            SpawnBossProjectile(dir);
        }
    }

    // Small helpers — extract the per-shot logic so future attacks reuse it.
    private void SpawnBossProjectile(Vector2 dir)
    {
        Vector3 spawnPos = transform.position + (Vector3)(dir * 0.8f);
        GameObject obj = Instantiate(bossProjectilePrefab, spawnPos, Quaternion.identity);
        Projectile p = obj.GetComponent<Projectile>();
        if (p != null) p.Configure(dir, bossProjectileSpeed, bossProjectileDamage, Team.Player);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    public void ServerHazard()
    {
        if (!IsServer || hazardPrefab == null) return;
        Transform target = GetNearestPlayer();
        if (target == null) return;

        // Spawn the hazard at the target's CURRENT position. They have ~1.2s (telegraph) to vacate.
        Vector3 spawnPos = target.position;
        GameObject obj = Instantiate(hazardPrefab, spawnPos, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    public void ServerBulletRing()
    {
        if (!IsServer || bossProjectilePrefab == null) return;

        float step = ringCount > 0 ? 360f / ringCount : 360f;
        for (int i = 0; i < ringCount; i++)
        {
            float angleDeg = ringStartAngle + step * i;
            float r = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            SpawnBossProjectile(dir);
        }
    }

    public void ServerLingeringHazard()
    {
        if (!IsServer || lingeringHazardPrefab == null) return;
        Transform target = GetNearestPlayer();
        if (target == null) return;

        Vector3 spawnPos = target.position;
        GameObject obj = Instantiate(lingeringHazardPrefab, spawnPos, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    public void ServerPullPlayers()
    {
        if (!IsServer) return;
        ApplyImpulseToAllPlayers(toward: true);
    }

    public void ServerPushPlayers()
    {
        if (!IsServer) return;
        ApplyImpulseToAllPlayers(toward: false);
    }

    private void ApplyImpulseToAllPlayers(bool toward)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge == null) continue;

            Vector2 dir = ((Vector2)(client.PlayerObject.transform.position - transform.position)).normalized;
            if (toward) dir = -dir;       // flip: pull means toward boss
            bridge.ServerApplyImpulse(dir, pullPushDistance, pullPushDuration);
        }
    }
}