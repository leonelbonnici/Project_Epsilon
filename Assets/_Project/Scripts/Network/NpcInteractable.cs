using Unity.Netcode;
using UnityEngine;
using PixelCrushers.DialogueSystem;

// Talkable NPC. Networking pattern:
//   1. Player presses Q → PlayerInteract calls ServerOnInteract
//   2. Server validates (NPC not locked, not already done if oneTime)
//   3. Server flags NPC as "in conversation", sends RPC to interacting client to start dialogue
//   4. Client runs Pixel Crusher's Dialogue System locally
//   5. On conversation end, client RPCs back to server "done"
//   6. Server flips isCompleted (if oneTime) and clears the in-conversation lock
[RequireComponent(typeof(NetworkObject))]
public class NpcInteractable : NetworkBehaviour, IInteractable
{
    [UnityEngine.Tooltip("Unique identifier within the area.")]
    public string npcId = "npc_01";

    [UnityEngine.Tooltip("Pixel Crusher Dialogue System conversation name to start when interacted with.")]
    public string conversationName = "";

    [UnityEngine.Tooltip("If true, NPC becomes 'completed' after first conversation ends and can't be talked to again. If false, conversation is repeatable.")]
    public bool oneTime = true;

    [UnityEngine.Tooltip("Visual shown when NPC has been talked to (one-time NPCs only). Optional checkmark/icon.")]
    public GameObject completedVisual;

    [UnityEngine.Tooltip("Visual hidden while a conversation is active on any client. Optional (e.g., to dim the NPC, hide the InteractPrompt).")]
    public GameObject normalVisual;

    public event System.Action<NpcInteractable> ConversationEnded;

    // Server-authoritative state.
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isInConversation = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string NpcId => npcId;
    public bool IsCompleted => isCompleted.Value;
    public bool IsInConversation => isInConversation.Value;
    public bool IsAvailable => !isInConversation.Value && !(oneTime && isCompleted.Value);

    public override void OnNetworkSpawn()
    {
        isCompleted.OnValueChanged += HandleCompletedChanged;
        isInConversation.OnValueChanged += HandleInConversationChanged;
        ApplyVisualState();
    }

    public override void OnNetworkDespawn()
    {
        isCompleted.OnValueChanged -= HandleCompletedChanged;
        isInConversation.OnValueChanged -= HandleInConversationChanged;
    }

    // Called by PlayerInteract on the server when a player presses Q within range.
    public void ServerOnInteract(NetworkPlayMakerBridge interactor)
    {
        if (!IsServer) return;
        if (!IsAvailable) return;                       // already done (oneTime) or someone else is talking
        if (string.IsNullOrEmpty(conversationName)) return;
        if (interactor == null) return;

        isInConversation.Value = true;
        ulong interactorClientId = interactor.OwnerClientId;

        // Tell ONLY the interacting client to start the conversation locally.
        var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Single(interactorClientId, RpcTargetUse.Temp) } };
        StartConversationRpc(rpcParams);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartConversationRpc(RpcParams rpcParams = default)
    {
        // Runs only on the interacting client. Set up the conversation and listen for its end.
        DialogueManager.instance.conversationEnded += OnLocalConversationEnded;
        DialogueManager.StartConversation(conversationName, transform);
    }

    // Local handler on the interacting client.
    private void OnLocalConversationEnded(Transform actor)
    {
        DialogueManager.instance.conversationEnded -= OnLocalConversationEnded;
        EndConversationRpc();
    }

    // Client tells server "conversation done"; server updates state.
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void EndConversationRpc()
    {
        if (!IsServer) return;
        isInConversation.Value = false;
        if (oneTime) isCompleted.Value = true;
        ConversationEnded?.Invoke(this);
    }

    private void HandleCompletedChanged(bool prev, bool curr) => ApplyVisualState();
    private void HandleInConversationChanged(bool prev, bool curr) => ApplyVisualState();

    private void ApplyVisualState()
    {
        // Show "completed" visual only if NPC is one-time and done.
        if (completedVisual != null) completedVisual.SetActive(oneTime && isCompleted.Value);
        // Hide the "normal" visual (or prompt holder) while in conversation.
        if (normalVisual != null) normalVisual.SetActive(!isInConversation.Value);
    }
}