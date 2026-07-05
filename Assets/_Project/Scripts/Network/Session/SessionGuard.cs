using System.Text;
using Unity.Netcode;
using UnityEngine;

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
        string incomingPlayerId = ExtractPayload(req);

        // Bootstrap case: LobbyBridge not spawned yet. Only the host itself can connect.
        if (lobby == null)
        {
            bool isHost = req.ClientNetworkId == NetworkManager.Singleton.LocalClientId;
            res.Approved = isHost;
            res.CreatePlayerObject = false;
            if (!isHost)
            {
                res.Reason = "Session not ready yet, try again.";
                return;
            }
            if (!string.IsNullOrEmpty(incomingPlayerId))
            {
                PendingPlayerIds.Stash(req.ClientNetworkId, incomingPlayerId);
            }
            return;
        }

        if (string.IsNullOrEmpty(incomingPlayerId))
        {
            res.Approved = false;
            res.Reason = "Missing player identity.";
            return;
        }

        bool gameStarted = lobby.GameStarted.Value;
        bool matchesReservedSlot = lobby.HasReservedSlotFor(incomingPlayerId);
        bool lobbyFull = lobby.Slots.Count >= lobby.maxPlayers;

        // Case A — Reconnecting player
        if (matchesReservedSlot)
        {
            res.Approved = true;
            res.CreatePlayerObject = false;
            PendingPlayerIds.Stash(req.ClientNetworkId, incomingPlayerId);
            return;
        }

        // Case B — Fresh player, game already started
        if (gameStarted)
        {
            res.Approved = false;
            res.Reason = "Game already in progress — session locked.";
            return;
        }

        // Case C — Fresh player, lobby full
        if (lobbyFull)
        {
            res.Approved = false;
            res.Reason = "Lobby is full.";
            return;
        }

        // Case D — Fresh player, joining pre-game lobby
        res.Approved = true;
        res.CreatePlayerObject = false;
        PendingPlayerIds.Stash(req.ClientNetworkId, incomingPlayerId);
    }

    private static string ExtractPayload(NetworkManager.ConnectionApprovalRequest req)
    {
        if (req.Payload == null || req.Payload.Length == 0) return "";
        return Encoding.UTF8.GetString(req.Payload);
    }
}