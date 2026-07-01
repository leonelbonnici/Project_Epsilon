using System.Collections;
using UnityEngine;

public class BossAttack_Slam : BossAttackBase
{
    [UnityEngine.Tooltip("Radius around the boss that gets hit.")]
    public float slamRadius = 3f;
    [UnityEngine.Tooltip("Damage to each player caught in range.")]
    public float slamDamage = 20f;

    protected override IEnumerator DoExecute()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius);
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (IsValidTarget(target)) target.ServerApplyDamage(slamDamage);
        }
        yield break;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}