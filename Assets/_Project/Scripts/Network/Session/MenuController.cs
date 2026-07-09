using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    [Header("Waiting room widgets")]
    public GameObject screenWaitingRoom;
    public TMP_Text waitingRoomLabel;
    public Button waitingRoomLeaveButton;   // still allow them to bail out

    [Header("Screens")]
    public GameObject screenMainMenu;
    public GameObject screenLobby;

    [Header("Main Menu widgets")]
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField joinCodeInput;
    public Button quitButton;

    [Header("Lobby widgets")]
    public TMP_Text roomCodeLabel;
    public Transform playerListRoot;
    public GameObject playerRowPrefab;
    public Button readyButton;
    public TMP_Text readyButtonLabel;
    public Button startGameButton;
    public TMP_Text startGameLabel;
    public Button leaveButton;


    private bool isReturningToMenu = false;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientStopped += HandleClientStopped;
        else
            Debug.LogWarning("[MenuCtrl] NetworkManager.Singleton null in Start; can't subscribe to OnClientStopped");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientStopped -= HandleClientStopped;
    }

    // Fires on the host when it stops, AND on every client when it gets disconnected.
    private void HandleClientStopped(bool wasHost)
    {
        Debug.Log($"[MenuCtrl] OnClientStopped(wasHost={wasHost}) — returning to main menu");
        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (isReturningToMenu) return;   // guard against a double reload
        isReturningToMenu = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Bootstrap");
    }

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        readyButton.onClick.AddListener(OnReadyToggled);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
        
        waitingRoomLeaveButton.onClick.AddListener(OnLeaveClicked);

        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        screenMainMenu.SetActive(true);
        screenLobby.SetActive(false);
        if (screenWaitingRoom != null) screenWaitingRoom.SetActive(false);
    }

    private void ShowLobby()
    {
        screenMainMenu.SetActive(false);
        screenLobby.SetActive(true);
    }

    // --- Main Menu handlers ---

    private async void OnHostClicked()
    {
        // Delegate to your existing Building Block session-create method.
        // Rename to whatever the BB exposes in your project (or your thin wrapper around it).
        bool ok = await MultiplayerSessionManager.CreateSessionAsync();
        if (ok) ShowLobby();
    }

    private async void OnJoinClicked()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;
        bool ok = await MultiplayerSessionManager.JoinSessionAsync(code);
        if (ok) ShowLobby();
    }

    private void OnQuitClicked() => Application.Quit();

    // --- Lobby handlers ---

    private void OnReadyToggled()
    {
        if (LobbyBridge.Instance == null) { Debug.LogWarning("[Lobby] LobbyBridge.Instance null"); return; }
        bool now = LobbyBridge.Instance.IsLocalPlayerReady();
        LobbyBridge.Instance.SetReadyRpc(!now);
    }

    private void OnStartGameClicked()
    {
        if (LobbyBridge.Instance == null) return;
        LobbyBridge.Instance.RequestStartGameRpc();
    }
    
    public async void OnLeaveClicked()
    {
        Debug.Log("[MenuCtrl] OnLeaveClicked: starting Leave");
        await MultiplayerSessionManager.LeaveSessionAsync();
        Debug.Log("[MenuCtrl] OnLeaveClicked: LeaveSessionAsync returned");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[MenuCtrl] OnLeaveClicked: calling Shutdown");
            NetworkManager.Singleton.Shutdown();
            // OnClientStopped fires → ReturnToMainMenu → Bootstrap reload.
        }
        else
        {
            ReturnToMainMenu();   // already stopped; route manually
        }
    }

    // --- Per-frame lobby state polling ---

    private void UpdateLobbyView()
    {
        if (!screenLobby.activeSelf) return;
        if (LobbyBridge.Instance == null) return;

        // Room code — plug into your BB wrapper.
        roomCodeLabel.text = $"Code: {MultiplayerSessionManager.CurrentCode}";

        RebuildPlayerList();

        // Ready button label
        readyButtonLabel.text = LobbyBridge.Instance.IsLocalPlayerReady() ? "Unready" : "Ready";

        // Host-only Start Game button
        bool isHost = LobbyBridge.Instance.IsLocalPlayerHost();
        bool canStart = LobbyBridge.Instance.AllConnectedPlayersReady();
        startGameButton.gameObject.SetActive(isHost);
        startGameButton.interactable = canStart;
        startGameLabel.text = canStart
            ? "Start Game"
            : $"Waiting… ({LobbyBridge.Instance.ConnectedCount()} in lobby, need all ready)";

        // If the game has started, hide the lobby — gameplay scene UI takes over.
        if (LobbyBridge.Instance.GameStarted.Value)
        {
            screenLobby.SetActive(false);
        }
    }

    private void Update()
    {
        if (LobbyBridge.Instance == null)
        {
            if (!wasBridgeNullLastFrame)
            {
                Debug.Log("[MenuCtrl] LobbyBridge.Instance is now null in Update");
                lastLoggedState = null;  // reset so next state change re-logs
            }
            wasBridgeNullLastFrame = true;
            return;
        }

        if (wasBridgeNullLastFrame)
        {
            Debug.Log("[MenuCtrl] LobbyBridge.Instance is now NON-null in Update");
        }
        wasBridgeNullLastFrame = false;

        SlotState? myState = GetLocalSlotState();

        if (myState != lastLoggedState)
        {
            Debug.Log($"[MenuCtrl] Update: myState changed from {lastLoggedState} to {myState}");
            lastLoggedState = myState;
        }

        if (myState == SlotState.InLobby)
        {
            ShowLobby();
            UpdateLobbyView();
        }
        else if (myState == SlotState.WaitingToJoin)
        {
            ShowWaitingRoom();
            UpdateWaitingView();
        }
        else if (myState == SlotState.Playing)
        {
            screenMainMenu.SetActive(false);
            screenLobby.SetActive(false);
            screenWaitingRoom.SetActive(false);
        }
        else
        {
            if (screenLobby.activeSelf || screenWaitingRoom.activeSelf)
            {
                ShowMainMenu();
            }
        }
    }

    private bool wasBridgeNullLastFrame = false;
    private SlotState? lastLoggedState = null;

    private SlotState? GetLocalSlotState()
    {
        if (LobbyBridge.Instance == null) return null;
        if (LobbyBridge.Instance.Slots == null) return null;
        
        try
        {
            ulong localId = NetworkManager.Singleton?.LocalClientId ?? 0;
            foreach (var s in LobbyBridge.Instance.Slots)
            {
                if (s.clientId == localId) return s.state;
            }
        }
        catch (System.ObjectDisposedException)
        {
            // NetworkList has been disposed (post-shutdown) — treat as no slot.
            return null;
        }
        return null;
    }

    private void ShowWaitingRoom()
    {
        screenMainMenu.SetActive(false);
        screenLobby.SetActive(false);
        screenWaitingRoom.SetActive(true);
    }

    private void UpdateWaitingView()
    {
        string sceneName = "an unknown place";
        if (LobbyBridge.Instance != null)
        {
            var replicated = LobbyBridge.Instance.CurrentSceneReplicated.Value;
            var replicatedStr = replicated.ToString();
            if (!string.IsNullOrEmpty(replicatedStr)) sceneName = replicatedStr;
        }
        waitingRoomLabel.text = $"The party is currently in {sceneName}.\nYou'll rejoin when they return to the Hub.";
    }

    private void UpdateWaitingLabel()
    {
        string sceneName = "an unknown place";
        if (LobbyBridge.Instance != null)
        {
            var replicated = LobbyBridge.Instance.CurrentSceneReplicated.Value;
            var replicatedStr = replicated.ToString();
            Debug.Log($"[Lobby] UpdateWaitingLabel: replicated bytes length={replicated.Length}, ToString='{replicatedStr}'");
            if (!string.IsNullOrEmpty(replicatedStr)) sceneName = replicatedStr;
        }
        waitingRoomLabel.text = $"The party is currently in {sceneName}.\nYou'll rejoin when they return to the Hub.";
    }

    private void RebuildPlayerList()
    {
        if (playerListRoot == null || playerRowPrefab == null || LobbyBridge.Instance == null) return;
        foreach (Transform c in playerListRoot) Destroy(c.gameObject);

        for (int i = 0; i < LobbyBridge.Instance.Slots.Count; i++)
        {
            var slot = LobbyBridge.Instance.Slots[i];
            GameObject row = Instantiate(playerRowPrefab, playerListRoot);

            var label = row.GetComponentInChildren<TMP_Text>();
            var toggle = row.GetComponentInChildren<Toggle>();

            string suffix = "";
            if (slot.clientId == NetworkManager.ServerClientId) suffix += " (host)";
            switch (slot.state)
            {
                case SlotState.Disconnected: suffix += " [disconnected]"; break;
                case SlotState.WaitingToJoin: suffix += " [waiting to join]"; break;
            }
            if (label != null) label.text = $"{slot.name}{suffix}";
            if (toggle != null) toggle.isOn = slot.ready;

            // Host kick button visibility — only for the host viewing non-Playing rows
            if (LobbyBridge.Instance.IsLocalPlayerHost() &&
                (slot.state == SlotState.Disconnected || slot.state == SlotState.WaitingToJoin))
            {
                AddKickButton(row, i);
            }
        }
    }

    private void AddKickButton(GameObject row, int slotIndex)
    {
        // Assumes your PlayerRow.prefab has an optional "KickButton" child that's inactive by default.
        // Or spawn one via prefab reference. Simplest: just show a small ✕ button on the row.
        var kick = row.transform.Find("KickButton");
        if (kick == null) return;
        kick.gameObject.SetActive(true);
        var btn = kick.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => LobbyBridge.Instance.HostKickSlotRpc(slotIndex));
    }
}