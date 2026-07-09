using UnityEngine;
using Rewired;
using Unity.Netcode;

public class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject pauseMenuRoot;

    [Header("Input")]
    [SerializeField] private int rewiredPlayerId = 0;
    [SerializeField] private string pauseActionName = "Pause";

    [Header("Menu routing")]
    [SerializeField] private MenuController menuController;

    private RigidbodyConstraints2D savedConstraints;
    private bool hasSavedConstraints;

    private bool isOpen;

    private void Start()
    {
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        isOpen = false;
    }

    private void Update()
    {
        if (!ReInput.isReady) return;

        Player rp = ReInput.players.GetPlayer(rewiredPlayerId);
        if (rp == null) return;

        if (rp.GetButtonDown(pauseActionName))
            Toggle();
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        // Only allow pausing once we're actually in-game (local player spawned).
        if (GetLocalPlayer() == null) return;

        isOpen = true;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
        SetLocalPlayerPaused(true);
    }

    public void Close()
    {
        isOpen = false;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        SetLocalPlayerPaused(false);
    }

    private void SetLocalPlayerPaused(bool paused)
    {
        GameObject player = GetLocalPlayer();
        if (player == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (paused)
            {
                if (!hasSavedConstraints)
                {
                    savedConstraints = rb.constraints;
                    hasSavedConstraints = true;
                }
                rb.linearVelocity = Vector2.zero;   // Unity 6 renamed .velocity → .linearVelocity
                rb.angularVelocity = 0f;
                rb.constraints = savedConstraints | RigidbodyConstraints2D.FreezePosition;
            }
            else if (hasSavedConstraints)
            {
                rb.constraints = savedConstraints;
                hasSavedConstraints = false;
            }
        }

        string evt = paused ? "LOCAL_PAUSED" : "LOCAL_UNPAUSED";
        PlayMakerFSM[] fsms = player.GetComponents<PlayMakerFSM>();
        foreach (PlayMakerFSM fsm in fsms)
            fsm.SendEvent(evt);
    }

    private GameObject GetLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null) return null;
        NetworkObject po = nm.LocalClient.PlayerObject;
        return po != null ? po.gameObject : null;
    }

    public void QuitToDesktop()
    {
        Close(); // unfreezes/unpauses locally first, harmless
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown(); // clean disconnect so peers don't wait for timeout

    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    public void LeaveToMainMenu()
    {
        Close(); // unfreeze + unpause locally before we tear the network down

        if (menuController != null)
        {
            menuController.OnLeaveClicked();   // routes through the exact same leave path as the lobby's Leave button
        }
        else
        {
            // Fallback if the reference wasn't wired: do the minimal safe teardown.
            Debug.LogWarning("[PauseMenu] menuController not assigned; doing direct shutdown fallback.");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Bootstrap");
        }
    }
}