using Unity.Netcode;
using UnityEngine;

// Player interaction. Server-authoritative: client requests interact, server checks
// for nearby IInteractables and fires the closest one. Mirrors PlayerCombat's pattern.
public class PlayerInteract : NetworkBehaviour
{
    [UnityEngine.Tooltip("How close the player must be to interact with an object.")]
    public float interactRange = 1.5f;

    // Call from PlayMaker on interact key press.
    public void Interact()
    {
        if (!IsOwner) return;
        InteractRpc();
    }

    [Rpc(SendTo.Server)]
    private void InteractRpc()
    {
        // Server finds the nearest interactable within range and fires it.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);
        IInteractable nearest = null;
        float bestDist = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;
            float dist = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = interactable;
            }
        }

        if (nearest != null)
        {
            NetworkPlayMakerBridge bridge = GetComponent<NetworkPlayMakerBridge>();
            nearest.ServerOnInteract(bridge);
        }
    }

    // Visualize range in the scene view when player is selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}