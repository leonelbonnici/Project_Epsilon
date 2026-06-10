using Unity.Netcode;
using UnityEngine;

// Server-driven networked projectile. The server moves it and decides hits;
// NetworkTransform syncs its position to everyone.
[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    [UnityEngine.Tooltip("Seconds before it despawns on its own if it hits nothing.")]
    public float lifetime = 3f;

    private Vector2 direction = Vector2.up;
    private float speed = 12f;
    private float damage = 15f;
    private float spawnTime;

    // Set by the shooter on the server, right before Spawn().
    public void Configure(Vector2 dir, float spd, float dmg)
    {
        direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;
        speed = spd;
        damage = dmg;
    }

    public override void OnNetworkSpawn() => spawnTime = Time.time;

    private void Update()
    {
        if (!IsServer) return;                 // server moves it; clients get the synced position
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        if (Time.time - spawnTime >= lifetime) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;                 // server decides the hit
        BossBridge boss = other.GetComponentInParent<BossBridge>();
        if (boss != null)
        {
            boss.ServerApplyDamage(damage);
            Despawn();
        }
    }

    private void Despawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}