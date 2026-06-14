using Unity.Netcode;
using UnityEngine;

// A switch the player interacts with via key press. Toggles between off/on states.
// Each switch's state is server-authoritative; clients react to the NetworkVariable.
[RequireComponent(typeof(NetworkObject))]
public class PuzzleSwitch : NetworkBehaviour, IInteractable
{
    [UnityEngine.Tooltip("Unique identifier for this switch within its puzzle room.")]
    public string switchId = "switch_01";

    [UnityEngine.Tooltip("If true, the switch can be toggled back off after being activated. If false, one-way activation.")]
    public bool toggleable = false;

    [UnityEngine.Tooltip("Visual to show when the switch is ACTIVE. Can be the whole GameObject or a child (e.g., a glowing overlay).")]
    public GameObject activeVisual;

    [UnityEngine.Tooltip("Visual to show when the switch is INACTIVE. Often the 'off' sprite.")]
    public GameObject inactiveVisual;

    public event System.Action<PuzzleSwitch> StateChanged;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string SwitchId => switchId;
    public bool IsActive => isActive.Value;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += HandleStateChanged;
        ApplyVisualState(isActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        isActive.OnValueChanged -= HandleStateChanged;
    }

    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        if (!toggleable && isActive.Value) return;   // one-way switches ignore further interacts

        isActive.Value = !isActive.Value;
        StateChanged?.Invoke(this);
    }

    private void HandleStateChanged(bool prev, bool curr) => ApplyVisualState(curr);

    private void ApplyVisualState(bool active)
    {
        if (activeVisual != null) activeVisual.SetActive(active);
        if (inactiveVisual != null) inactiveVisual.SetActive(!active);
    }

    // Server-only: force the switch to a specific state. Used by PuzzleRoom for sequence resets.
    public void ServerSetActive(bool active)
    {
        if (!IsServer) return;
        if (isActive.Value == active) return;
        isActive.Value = active;
        StateChanged?.Invoke(this);
    }
}