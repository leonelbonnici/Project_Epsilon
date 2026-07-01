using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Base class for a single boss attack. Owns its own params, telegraph timing,
// and broadcast events. Server-authoritative via the parent BossBridge —
// attack scripts themselves don't need to be NetworkBehaviours.
public abstract class BossAttackBase : MonoBehaviour
{
    [Header("Telegraph")]

    [UnityEngine.Tooltip("Seconds of telegraph before the attack fires. 0 = fire immediately.")]
    [Range(0f, 5f)]
    public float telegraphDuration = 0.5f;

    [UnityEngine.Tooltip("PlayMaker event fired (on all clients) at telegraph start. Empty = no broadcast. Wire a presentation FSM to this for visuals/sound.")]
    public string telegraphStartedEvent = "";

    [UnityEngine.Tooltip("PlayMaker event fired (on all clients) when the attack executes (after telegraph). Empty = no broadcast.")]
    public string executedEvent = "";

    // Runtime reference to the boss on the same GameObject. Used for IsServer checks
    // and to route PlayMaker event broadcasts through the networked substrate.
    protected BossBridge boss;

    protected virtual void Awake()
    {
        boss = GetComponent<BossBridge>();
    }

    // Public entry point — the selector calls this. Do NOT override.
    public void ServerExecute()
    {
        if (boss == null || !boss.IsServer) return;
        StartCoroutine(ExecuteRoutine());
    }

    private IEnumerator ExecuteRoutine()
    {
        if (telegraphDuration > 0f)
        {
            BroadcastPresentationEvent(telegraphStartedEvent);
            yield return new WaitForSeconds(telegraphDuration);
        }

        yield return DoExecute();

        BroadcastPresentationEvent(executedEvent);
        // NEW: always fire the generic "attack cycle finished" event for the brain FSM.
        boss.ServerBroadcastEvent("ATTACK_DONE");
    }

    // Concrete attacks implement this. For instant attacks, end with `yield break;`.
    protected abstract IEnumerator DoExecute();

    protected void BroadcastPresentationEvent(string eventName)
    {
        if (boss == null || string.IsNullOrEmpty(eventName)) return;
        boss.ServerBroadcastEvent(eventName);
    }

    // --- Shared targeting helpers ---

    protected static bool IsValidTarget(IDamageable target)
    {
        if (target == null || target.Team != Team.Player) return false;
        var bridge = target as NetworkPlayMakerBridge;
        if (bridge != null && bridge.IsDowned) return false;
        return true;
    }

    protected Transform GetNearestPlayer()
    {
        Transform nearest = null;
        float best = float.MaxValue;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject po = client.PlayerObject;
            if (po == null) continue;
            var bridge = po.GetComponent<NetworkPlayMakerBridge>();
            if (bridge != null && bridge.IsDowned) continue;

            float d = ((Vector2)(po.transform.position - transform.position)).sqrMagnitude;
            if (d < best) { best = d; nearest = po.transform; }
        }
        return nearest;
    }

    protected void SpawnBossProjectile(Vector2 dir, GameObject prefab, float speed, float damage)
    {
        if (prefab == null) return;
        Vector3 spawnPos = transform.position + (Vector3)(dir * 0.8f);
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        var p = obj.GetComponent<Projectile>();
        if (p != null) p.Configure(dir, speed, damage, Team.Player);
        obj.GetComponent<NetworkObject>().Spawn();
    }

    protected static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}