using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossAttack_Dash : BossAttackBase
{
    public float dashDistance = 6f;
    public float dashDuration = 0.4f;
    public float dashDamage = 25f;
    public float dashRadius = 1.2f;

    protected override IEnumerator DoExecute()
    {
        Transform target = GetNearestPlayer();
        if (target == null) yield break;

        Vector2 direction = ((Vector2)(target.position - transform.position)).normalized;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (Vector3)(direction * dashDistance);
        float elapsed = 0f;
        var hitPlayers = new HashSet<IDamageable>();

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, dashRadius);
            foreach (Collider2D hit in hits)
            {
                IDamageable d = hit.GetComponentInParent<IDamageable>();
                if (IsValidTarget(d) && !hitPlayers.Contains(d))
                {
                    d.ServerApplyDamage(dashDamage);
                    hitPlayers.Add(d);
                }
            }
            yield return null;
        }
        transform.position = endPos;
    }
}