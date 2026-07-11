using UnityEngine;
using Rewired;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
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

    [Header("Room code")]
    [SerializeField] private TMP_Text roomCodeLabel;

    [Header("Party panel")]
    [SerializeField] private Transform partyListRoot;
    [SerializeField] private GameObject playerRowPrefab;

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
        if (GetLocalPlayer() == null) return;

        isOpen = true;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);

        RefreshRoomCode();
        RebuildPartyList();          // ← add this

        SetLocalPlayerPaused(true);

        if (LobbyBridge.Instance != null && LobbyBridge.Instance.Slots != null)
        LobbyBridge.Instance.Slots.OnListChanged += OnSlotsChangedWhilePaused;
    }

    private void RebuildPartyList()
    {
        if (partyListRoot == null || playerRowPrefab == null) return;
        if (LobbyBridge.Instance == null) return;

        // Clear old rows
        foreach (Transform c in partyListRoot) Destroy(c.gameObject);

        var bridge = LobbyBridge.Instance;
        bool isHost = bridge.IsLocalPlayerHost();

        for (int i = 0; i < bridge.Slots.Count; i++)
        {
            var slot = bridge.Slots[i];
            GameObject row = Instantiate(playerRowPrefab, partyListRoot);

            var label = row.GetComponentInChildren<TMP_Text>();
            var toggle = row.GetComponentInChildren<UnityEngine.UI.Toggle>();

            string suffix = "";
            if (slot.clientId == NetworkManager.ServerClientId) suffix += " (host)";
            switch (slot.state)
            {
                case SlotState.Disconnected:  suffix += " [disconnected]"; break;
                case SlotState.WaitingToJoin: suffix += " [waiting to join]"; break;
            }
            if (label != null) label.text = $"{slot.name}{suffix}";

            // The lobby prefab has a ready Toggle; mid-game it's just noise. Hide it.
            if (toggle != null) toggle.gameObject.SetActive(false);

            // Host-only kick button, on every slot EXCEPT the host's own row.
            bool canKick = isHost && slot.clientId != NetworkManager.ServerClientId;
            SetupKickButton(row, i, canKick);
        }
    }

    private void SetupKickButton(GameObject row, int slotIndex, bool canKick)
    {
        // GetComponentInChildren is recursive (Transform.Find is NOT — it only
        // checks direct children, which is why the nested kick button was missed).
        // The row has exactly one Button (the kick 'x'), so this finds it safely.
        var btn = row.GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (btn == null) return;

        btn.gameObject.SetActive(canKick);
        if (!canKick) return;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (LobbyBridge.Instance != null)
                LobbyBridge.Instance.HostKickSlotRpc(slotIndex);
            Invoke(nameof(RebuildPartyList), 0.15f);
        });
    }

    private void RefreshRoomCode()
    {
        if (roomCodeLabel == null) return;
        string code = MultiplayerSessionManager.CurrentCode;
        roomCodeLabel.text = string.IsNullOrEmpty(code) ? "Room: —" : $"Room: {code}";
    }

    public void Close()
    {
        isOpen = false;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);

        if (LobbyBridge.Instance != null && LobbyBridge.Instance.Slots != null)
            LobbyBridge.Instance.Slots.OnListChanged -= OnSlotsChangedWhilePaused;

        SetLocalPlayerPaused(false);
    }

    private void OnSlotsChangedWhilePaused(NetworkListEvent<PartySlot> _)
    {
        if (isOpen) RebuildPartyList();
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