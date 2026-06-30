using UnityEngine;

public class DownedStateController : MonoBehaviour
{
    [UnityEngine.Tooltip("Names of PlayMaker FSMs on this GameObject to disable while downed.")]
    public string[] fsmNamesToDisable;

    [UnityEngine.Tooltip("Other MonoBehaviour components to disable while downed (e.g., PlayerInteract).")]
    public MonoBehaviour[] componentsToDisable;

    [UnityEngine.Tooltip("If true, zero out Rigidbody2D velocity when entering downed.")]
    public bool zeroVelocityOnDown = true;

    private NetworkPlayMakerBridge bridge;

    private void Awake()
    {
        bridge = GetComponent<NetworkPlayMakerBridge>();
    }

    private void OnEnable()
    {
        if (bridge != null) bridge.DownedChanged += OnDownedChanged;
    }

    private void OnDisable()
    {
        if (bridge != null) bridge.DownedChanged -= OnDownedChanged;
    }

    private void OnDownedChanged(bool isDowned)
    {
        Debug.Log($"[DownedStateController] {gameObject.name} OnDownedChanged({isDowned})");
        SetGateActive(!isDowned);

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (isDowned)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }

        private void SetGateActive(bool active)
    {
        int affected = 0;
        if (fsmNamesToDisable != null)
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                foreach (var name in fsmNamesToDisable)
                {
                    if (fsm.FsmName == name)
                    {
                        fsm.enabled = active;
                        // On revive, also send a PLAYER_REVIVED event so the FSM can hard-reset its state.
                        if (active) fsm.SendEvent("PLAYER_REVIVED");
                        affected++;
                        break;
                    }
                }
            }
        }

        if (componentsToDisable != null)
        {
            foreach (var comp in componentsToDisable)
            {
                if (comp != null) comp.enabled = active;
            }
        }
    }
}