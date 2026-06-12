using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    [UnityEngine.Tooltip("Seconds before it despawns on its own if it hits nothing.")]
    public float lifetime = 3f;

    private Vector2 direction = Vector2.up;
    private float speed = 12f;
    private float damage = 15f;
    private Team targetTeam = Team.Enemy;
    private float spawnTime;

    public void Configure(Vector2 dir, float spd, float dmg, Team targetTeam = Team.Enemy)
    {
        direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;
        speed = spd;
        damage = dmg;
        this.targetTeam = targetTeam;
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
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null && target.Team == targetTeam)
        {
            target.ServerApplyDamage(damage);
            Despawn();
        }
    }

    private void Despawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn();
    }
}