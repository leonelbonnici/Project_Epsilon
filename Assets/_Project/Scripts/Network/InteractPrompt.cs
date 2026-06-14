using Unity.Netcode;
using UnityEngine;

// Client-driven proximity prompt. Each client checks its OWN local player's distance
// and toggles its local visual. No networking — interactable state comes from existing
// NetworkVariables on the IInteractable component itself.
public class InteractPrompt : MonoBehaviour
{
    [UnityEngine.Tooltip("Range within which the local player triggers the prompt to show.")]
    public float promptRange = 1.5f;

    [UnityEngine.Tooltip("How often (seconds) to check for local player proximity. Lower = more responsive, higher = cheaper.")]
    public float pollInterval = 0.15f;

    [UnityEngine.Tooltip("Visual to show when the local player is nearby. Hidden by default.")]
    public GameObject promptVisual;

    [UnityEngine.Tooltip("If the interactable becomes 'inactive' (collected, completed, etc.), hide the prompt.")]
    public bool autoHideWhenInactive = true;

    private float pollTimer = 0f;
    private Transform localPlayerTransform;
    private bool currentlyVisible = false;

    private void Start()
    {
        ApplyPromptState(false);
    }

    private void Update()
    {
        pollTimer += Time.deltaTime;
        if (pollTimer < pollInterval) return;
        pollTimer = 0f;

        // Local player may not exist yet at scene start; keep trying.
        if (localPlayerTransform == null) localPlayerTransform = FindLocalPlayer();
        if (localPlayerTransform == null) return;

        bool playerNearby = ((Vector2)(localPlayerTransform.position - transform.position)).sqrMagnitude <= promptRange * promptRange;
        bool interactableActive = IsThisInteractableActive();
        bool show = playerNearby && interactableActive;

        if (show != currentlyVisible)
        {
            currentlyVisible = show;
            ApplyPromptState(show);
        }
    }

    private Transform FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return null;
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return null;
        return localClient.PlayerObject.transform;
    }

    private bool IsThisInteractableActive()
    {
        if (!autoHideWhenInactive) return true;

        var pickup = GetComponent<PickupCollectible>();
        if (pickup != null) return !pickup.IsCollected;

        var sw = GetComponent<PuzzleSwitch>();
        if (sw != null && !sw.toggleable) return !sw.IsActive;

        var npc = GetComponent<NpcInteractable>();
        if (npc != null) return npc.IsAvailable;

        return true;
    }

    private void ApplyPromptState(bool show)
    {
        if (promptVisual != null) promptVisual.SetActive(show);
    }
}