using Unity.Netcode;
using UnityEngine;

// Server-driven projectile. Reused for player shots (hit the boss) and boss shots
// (hit players), controlled by hitsPlayers.
[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    [UnityEngine.Tooltip("Seconds before it despawns on its own if it hits nothing.")]
    public float lifetime = 3f;

    private Vector2 direction = Vector2.up;
    private float speed = 12f;
    private float damage = 15f;
    private bool hitsPlayers = false;
    private float spawnTime;

    public void Configure(Vector2 dir, float spd, float dmg, bool hitsPlayers = false)
    {
        direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;
        speed = spd;
        damage = dmg;
        this.hitsPlayers = hitsPlayers;
    }

    public override void OnNetworkSpawn() => spawnTime = Time.time;

    private void Update()
    {
        if (!IsServer) return;
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        if (Time.time - spawnTime >= lifetime) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (hitsPlayers)
        {
            NetworkPlayMakerBridge player = other.GetComponentInParent<NetworkPlayMakerBridge>();
            if (player != null) { player.ServerApplyDamage(damage); Despawn(); }
        }
        else
        {
            BossBridge boss = other.GetComponentInParent<BossBridge>();
            if (boss != null) { boss.ServerApplyDamage(damage); Despawn(); }
        }
    }

    private void Despawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}