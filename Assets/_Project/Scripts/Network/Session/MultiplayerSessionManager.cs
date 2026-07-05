using System;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Netcode;

// Thin wrapper around Unity's Multiplayer Services SDK. Replaces the Building Block's
// UXML-based Quick Join UI with an API we call from our own uGUI MenuController.
// Handles: create host session, join by code, leave. Everything else (NGO handshake,
// player spawning, Relay allocation) is done by the SDK internally when
// createNetworkSession = true on the SessionSettings asset.
public class MultiplayerSessionManager : MonoBehaviour
{
    public static MultiplayerSessionManager Instance { get; private set; }

    [UnityEngine.Tooltip("The SessionSettings ScriptableObject that drives session options. Same asset the Building Block's QuickJoin.uxml was using — reuse it or create a new one via Create > Services > Blocks > Session > SessionSettings.")]
    public SessionSettings sessionSettings;

    // Cached last error message for the UI to display if needed.
    public static string LastError { get; private set; } = "";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private async void OnApplicationQuit()
    {
        if (GetCurrentSession() == null) return;   // nothing to leave
        Debug.Log("[MultiplayerSessionManager] OnApplicationQuit — leaving session");
        try
        {
            await LeaveSessionAsync();
        }
        catch (System.ObjectDisposedException)
        {
            // Already disposed by explicit Leave — fine.
        }
    }

    // --- Public API ---

    // Session code for the current session (empty if no session).
    public static string CurrentCode
    {
        get
        {
            var s = GetCurrentSession();
            return s != null ? s.Code : "";
        }
    }

    // True if we're currently in a session (either as host or client).
    public static bool InSession => GetCurrentSession() != null;

    private static void StampConnectionPayload()
    {
        if (NetworkManager.Singleton == null) return;
        if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn) return;

        string playerId = AuthenticationService.Instance.PlayerId ?? "";
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(playerId);
    }

    public static async Task<bool> CreateSessionAsync()
    {
        StampConnectionPayload();

        if (!EnsureReady()) return false;

        try
        {
            var options = Instance.sessionSettings.ToSessionOptions();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[MultiplayerSessionManager] Created session, code = {session?.Code}");
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[MultiplayerSessionManager] CreateSession failed: {e.Message}");
            return false;
        }
    }

    public static async Task<bool> JoinSessionAsync(string code)
    {
        StampConnectionPayload();
        
        if (!EnsureReady()) return false;
        if (string.IsNullOrWhiteSpace(code)) { LastError = "Empty session code."; return false; }

        try
        {
            var options = Instance.sessionSettings.ToJoinSessionOptions();
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim(), options);
            Debug.Log($"[MultiplayerSessionManager] Joined session, code = {session?.Code}");
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[MultiplayerSessionManager] JoinSession failed: {e.Message}");
            return false;
        }
    }

    public static async Task LeaveSessionAsync()
    {
        var s = GetCurrentSession();
        if (s == null) return;

        try
        {
            await s.LeaveAsync();
            Debug.Log("[MultiplayerSessionManager] Left session.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiplayerSessionManager] LeaveSession failed: {e.Message}");
        }
    }

    // --- Internal ---

    private static bool EnsureReady()
    {
        if (Instance == null)
        {
            LastError = "MultiplayerSessionManager not present in scene.";
            Debug.LogError(LastError);
            return false;
        }
        if (Instance.sessionSettings == null)
        {
            LastError = "SessionSettings not assigned on MultiplayerSessionManager.";
            Debug.LogError(LastError);
            return false;
        }
        if (MultiplayerService.Instance == null)
        {
            LastError = "Unity Multiplayer Services not initialized (missing UnityServicesWithName in scene?).";
            Debug.LogError(LastError);
            return false;
        }
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            LastError = "Unity Services not fully initialized yet.";
            Debug.LogError(LastError);
            return false;
        }
        return true;
    }

    private static ISession GetCurrentSession()
    {
        if (Instance == null || Instance.sessionSettings == null) return null;
        if (MultiplayerService.Instance == null) return null;
        return MultiplayerService.Instance.Sessions.TryGetValue(Instance.sessionSettings.sessionType, out var s) ? s : null;
    }
}