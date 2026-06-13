using Unity.Netcode;
using UnityEngine;

// Server-authoritative pickup. Collected state lives in a NetworkVariable so all clients
// see the same state without anything getting destroyed. Same pattern as Door.isLocked.
[RequireComponent(typeof(NetworkObject))]
public class PickupCollectible : NetworkBehaviour, IInteractable
{
    [UnityEngine.Tooltip("Unique identifier for this pickup within its room.")]
    public string pickupId = "pickup_01";

    [UnityEngine.Tooltip("Points awarded to the shared score on collection. 0 if non-score.")]
    public int scoreValue = 0;

    [UnityEngine.Tooltip("Visual child to hide when collected. If null, hides this GameObject's SpriteRenderer.")]
    public GameObject visual;

    public event System.Action<PickupCollectible> CollectedRaised;

    private NetworkVariable<bool> isCollected = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string PickupId => pickupId;
    public bool IsCollected => isCollected.Value;

    public override void OnNetworkSpawn()
    {
        isCollected.OnValueChanged += HandleCollectedChanged;
        ApplyCollectedState(isCollected.Value);
    }

    public override void OnNetworkDespawn()
    {
        isCollected.OnValueChanged -= HandleCollectedChanged;
    }

    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        if (isCollected.Value) return;       // already collected, ignore further interacts

        if (scoreValue > 0)
        {
            GameStateBridge gm = FindFirstObjectByType<GameStateBridge>();
            if (gm != null) gm.ServerAddScore(scoreValue);
        }

        isCollected.Value = true;
        CollectedRaised?.Invoke(this);
    }

    private void HandleCollectedChanged(bool prev, bool curr) => ApplyCollectedState(curr);

    private void ApplyCollectedState(bool collected)
    {
        // Hide visual on every client (NetworkVariable change fires this everywhere).
        if (visual != null)
        {
            visual.SetActive(!collected);
        }
        else
        {
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = !collected;
        }
        // Disable the collider so InteractRpc's overlap query doesn't find it again.
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = !collected;
    }
}