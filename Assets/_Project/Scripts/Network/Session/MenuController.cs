using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuController : MonoBehaviour
{
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

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        readyButton.onClick.AddListener(OnReadyToggled);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);

        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        screenMainMenu.SetActive(true);
        screenLobby.SetActive(false);
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
    
    private async void OnLeaveClicked()
    {
        await MultiplayerSessionManager.LeaveSessionAsync();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        ShowMainMenu();
    }

    // --- Per-frame lobby state polling ---

    private void Update()
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

    private void RebuildPlayerList()
    {
        if (playerListRoot == null || playerRowPrefab == null) return;
        if (LobbyBridge.Instance == null) return;

        foreach (Transform c in playerListRoot) Destroy(c.gameObject);

        foreach (var slot in LobbyBridge.Instance.Slots)
        {
            GameObject row = Instantiate(playerRowPrefab, playerListRoot);
            var lbl = row.GetComponentInChildren<TMP_Text>();
            var toggle = row.GetComponentInChildren<Toggle>();

            if (lbl != null)
            {
                string suffix = "";
                if (slot.clientId == NetworkManager.ServerClientId) suffix = " (host)";
                if (!slot.connected) suffix += " (disconnected)";
                lbl.text = $"{slot.name}{suffix}";
            }
            if (toggle != null) toggle.isOn = slot.ready;
        }
    }
}