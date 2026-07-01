using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossAttack_PushPlayers : BossAttackBase
{
    public float pushDistance = 4f;
    public float pushDuration = 0.35f;

    protected override IEnumerator DoExecute()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge == null || bridge.IsDowned) continue;

            Vector2 awayFromBoss = ((Vector2)(client.PlayerObject.transform.position - transform.position)).normalized;
            bridge.ServerApplyImpulse(awayFromBoss, pushDistance, pushDuration);
        }
        yield break;
    }
}