using System.Text;
using Unity.Netcode;
using UnityEngine;

// Approval-time policy for accepting/rejecting incoming connections.
// - Blocks joins beyond max party size.
// - Blocks late joins during an active game.
// - Allows reconnects only when the server is in a safe state (Hub, not transitioning).
public class SessionGuard : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.ConnectionApprovalCallback += Approve;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.ConnectionApprovalCallback -= Approve;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        var lobby = LobbyBridge.Instance;
        if (lobby == null)
        {
            // Lobby not ready yet — approve the host (which triggers spawn of the LobbyBridge),
            // reject anything else.
            bool isHost = req.ClientNetworkId == NetworkManager.Singleton.LocalClientId;
            res.Approved = isHost;
            res.CreatePlayerObject = isHost;
            if (!isHost) res.Reason = "Session not ready yet, try again.";
            return;
        }

        // Read the client-provided payload — used for reconnection matching.
        // For Track A this is just a player name; later, swap for a stable playerId.
        string incomingName = req.Payload != null && req.Payload.Length > 0
            ? Encoding.UTF8.GetString(req.Payload)
            : $"Player {req.ClientNetworkId}";

        bool gameStarted = lobby.GameStarted.Value;
        bool lobbyFull = lobby.Slots.Count >= lobby.maxPlayers;
        bool isReconnect = gameStarted && lobby.HasDisconnectedSlotFor(incomingName);

        // Case A — Reconnecting player
        if (isReconnect)
        {
            if (lobby.IsReconnectAllowedNow())
            {
                res.Approved = true;
                res.CreatePlayerObject = false;
            }
            else
            {
                res.Approved = false;
                res.Reason = "Session is in an active encounter — try again shortly.";
            }
            return;
        }

        // Case B — New player, game already started
        if (gameStarted)
        {
            res.Approved = false;
            res.Reason = "Game already in progress — session locked.";
            return;
        }

        // Case C — New player, lobby full
        if (lobbyFull)
        {
            res.Approved = false;
            res.Reason = "Lobby is full.";
            return;
        }

        // Case D — New player, lobby has room
        res.Approved = true;
        res.CreatePlayerObject = false;
    }
}