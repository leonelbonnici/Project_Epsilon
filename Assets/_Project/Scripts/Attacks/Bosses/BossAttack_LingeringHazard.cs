using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossAttack_LingeringHazard : BossAttackBase
{
    public GameObject lingeringHazardPrefab;

    protected override IEnumerator DoExecute()
    {
        Transform target = GetNearestPlayer();
        if (target == null || lingeringHazardPrefab == null) yield break;

        GameObject obj = Instantiate(lingeringHazardPrefab, target.position, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn();
    }
}