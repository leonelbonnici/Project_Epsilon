using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BossAttackOption
{
    [UnityEngine.Tooltip("Global PlayMaker event that triggers this attack's FSM, e.g. DO_SLAM.")]
    public string eventName;
    [UnityEngine.Tooltip("Relative likelihood of being chosen.")]
    public float weight = 1f;
    [UnityEngine.Tooltip("Only eligible once the boss is at this phase or higher.")]
    public int minPhase = 0;
}

public class BossAttackSelector : MonoBehaviour
{
    [UnityEngine.Tooltip("The attacks this boss can use. Each fires a PlayMaker event by name.")]
    public List<BossAttackOption> attacks = new List<BossAttackOption>();

    private BossBridge boss;
    private PlayMakerFSM[] fsms;

    private void Awake()
    {
        boss = GetComponent<BossBridge>();
        fsms = GetComponents<PlayMakerFSM>();
    }

    // Called by the brain FSM on the server. Picks a phase-eligible attack by weight and fires its event.
    public void ServerChooseAndFire()
    {
        int phase = boss != null ? boss.PhaseValue : 0;

        float total = 0f;
        foreach (var a in attacks)
            if (a.minPhase <= phase) total += Mathf.Max(0f, a.weight);
        if (total <= 0f) return;

        float roll = Random.value * total;
        foreach (var a in attacks)
        {
            if (a.minPhase > phase) continue;
            roll -= Mathf.Max(0f, a.weight);
            if (roll <= 0f) { Fire(a.eventName); return; }
        }
    }

    private void Fire(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in fsms) fsm.SendEvent(eventName);
    }
}