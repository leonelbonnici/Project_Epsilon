using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossAttack_Hazard : BossAttackBase
{
    public GameObject hazardPrefab;

    protected override IEnumerator DoExecute()
    {
        Transform target = GetNearestPlayer();
        if (target == null || hazardPrefab == null) yield break;

        GameObject obj = Instantiate(hazardPrefab, target.position, Quaternion.identity);
        obj.GetComponent<NetworkObject>().Spawn();
    }
}