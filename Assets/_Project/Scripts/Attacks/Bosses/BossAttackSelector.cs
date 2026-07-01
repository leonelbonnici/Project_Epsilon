using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BossAttackOption
{
    [UnityEngine.Tooltip("Attack component (must be on the same GameObject as this selector).")]
    public BossAttackBase attack;
    [UnityEngine.Tooltip("Relative likelihood of being chosen among currently-eligible attacks.")]
    public float weight = 1f;
    [UnityEngine.Tooltip("Only eligible once the boss is at this phase or higher.")]
    public int minPhase = 0;
}

public class BossAttackSelector : MonoBehaviour
{
    [UnityEngine.Tooltip("Attack pool for this boss. Each entry references a BossAttackBase component with a weight and phase gate.")]
    public List<BossAttackOption> attacks = new List<BossAttackOption>();

    private BossBridge boss;

    private void Awake() { boss = GetComponent<BossBridge>(); }

    // Called by the brain FSM on the server every attack cycle.
    public void ServerChooseAndFire()
    {
        if (boss == null || !boss.IsServer) return;
        int currentPhase = boss.PhaseValue;

        // First pass: sum weights of eligible attacks.
        float total = 0f;
        foreach (var o in attacks)
        {
            if (o.attack == null || o.minPhase > currentPhase) continue;
            total += Mathf.Max(0f, o.weight);
        }
        if (total <= 0f) return;

        // Second pass: weighted roll.
        float roll = Random.value * total;
        foreach (var o in attacks)
        {
            if (o.attack == null || o.minPhase > currentPhase) continue;
            roll -= Mathf.Max(0f, o.weight);
            if (roll <= 0f) { o.attack.ServerExecute(); return; }
        }
    }
}