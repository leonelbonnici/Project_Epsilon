using Unity.Netcode;
using UnityEngine;

public class HealInteractable : NetworkBehaviour, IInteractable
{
    [UnityEngine.Tooltip("Fired when the heal is performed (for FX, sounds, etc.).")]
    public string HealedEvent = "HEAL_TRIGGERED";

    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        Debug.Log($"[HealInteractable] ServerOnInteract fired by client {interactor.OwnerClientId}");

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var bridge = client.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
            if (bridge != null)
            {
                Debug.Log($"[HealInteractable] Healing client {client.ClientId} (health was {bridge.HealthValue})");
                bridge.ServerHealFull();
            }
        }

        BroadcastEventRpc(HealedEvent);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEventRpc(Unity.Collections.FixedString64Bytes e)
        => SendEventToAllFsms(e.ToString());

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.SendEvent(eventName);
    }
}