using System.Collections;
using UnityEngine;

public class BossAttack_Shoot : BossAttackBase
{
    public GameObject projectilePrefab;
    public float projectileSpeed = 8f;
    public float projectileDamage = 15f;

    protected override IEnumerator DoExecute()
    {
        Transform target = GetNearestPlayer();
        if (target == null) yield break;
        Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
        SpawnBossProjectile(dir, projectilePrefab, projectileSpeed, projectileDamage);
    }
}