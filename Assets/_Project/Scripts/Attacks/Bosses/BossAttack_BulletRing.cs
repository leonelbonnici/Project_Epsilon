using System.Collections;
using UnityEngine;

public class BossAttack_BulletRing : BossAttackBase
{
    public GameObject projectilePrefab;
    public float projectileSpeed = 8f;
    public float projectileDamage = 15f;
    public int ringCount = 12;
    public float ringStartAngle = 0f;

    protected override IEnumerator DoExecute()
    {
        float step = ringCount > 0 ? 360f / ringCount : 360f;
        for (int i = 0; i < ringCount; i++)
        {
            float angleDeg = ringStartAngle + step * i;
            float r = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            SpawnBossProjectile(dir, projectilePrefab, projectileSpeed, projectileDamage);
        }
        yield break;
    }
}