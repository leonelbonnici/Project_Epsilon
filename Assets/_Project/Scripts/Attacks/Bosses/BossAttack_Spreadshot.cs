using System.Collections;
using UnityEngine;

public class BossAttack_SpreadShot : BossAttackBase
{
    public GameObject projectilePrefab;
    public float projectileSpeed = 8f;
    public float projectileDamage = 15f;
    public int spreadCount = 3;
    public float spreadAngle = 30f;

    protected override IEnumerator DoExecute()
    {
        Transform target = GetNearestPlayer();
        if (target == null) yield break;

        Vector2 baseDir = ((Vector2)(target.position - transform.position)).normalized;
        float halfSpread = spreadAngle * 0.5f;
        float step = spreadCount > 1 ? spreadAngle / (spreadCount - 1) : 0f;

        for (int i = 0; i < spreadCount; i++)
        {
            float offsetDeg = -halfSpread + step * i;
            Vector2 dir = Rotate(baseDir, offsetDeg);
            SpawnBossProjectile(dir, projectilePrefab, projectileSpeed, projectileDamage);
        }
    }
}