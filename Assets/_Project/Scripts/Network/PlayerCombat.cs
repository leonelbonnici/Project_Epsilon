using Rewired;
using Unity.Netcode;
using UnityEngine;

// Player attacks. Server-authoritative: the client requests, the server evaluates.
public class PlayerCombat : NetworkBehaviour
{
    [UnityEngine.Tooltip("Rewired player id (your game player = 0).")]
    public int rewiredPlayerId = 0;

    [UnityEngine.Tooltip("Melee damage per swing.")]
    public float meleeDamage = 25f;
    [UnityEngine.Tooltip("Melee reach — radius around the player.")]
    public float meleeRange = 1.5f;

    [UnityEngine.Tooltip("Networked projectile prefab (must be in the NetworkPrefabs list).")]
    public GameObject projectilePrefab;
    [UnityEngine.Tooltip("Projectile travel speed.")]
    public float projectileSpeed = 12f;
    [UnityEngine.Tooltip("Ranged damage per hit.")]
    public float rangedDamage = 15f;

    private Player rewired;

    private void Start()
    {
        if (ReInput.isReady) rewired = ReInput.players.GetPlayer(rewiredPlayerId);
    }

    // ---- Melee ----
    public void Melee()
    {
        if (!IsOwner) return;
        MeleeRpc();
    }

    [Rpc(SendTo.Server)]
    private void MeleeRpc()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, meleeRange);
        foreach (Collider2D hit in hits)
        {
            BossBridge boss = hit.GetComponentInParent<BossBridge>();
            if (boss != null) boss.ServerApplyDamage(meleeDamage);
        }
    }

    // ---- Ranged ----
    public void RangedFire()
    {
        if (!IsOwner) return;
        FireRpc(transform.position, AimDirection());
    }

    private Vector2 AimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.up;
        Vector3 world = cam.ScreenToWorldPoint(ReInput.controllers.Mouse.screenPosition);
        Vector2 dir = (Vector2)world - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(Vector3 origin, Vector2 direction)
    {
        if (projectilePrefab == null) return;
        Vector3 spawnPos = origin + (Vector3)(direction * 0.6f);   // a little ahead of the player
        GameObject obj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        Projectile p = obj.GetComponent<Projectile>();
        if (p != null) p.Configure(direction, projectileSpeed, rangedDamage);
        obj.GetComponent<NetworkObject>().Spawn();
    }
}