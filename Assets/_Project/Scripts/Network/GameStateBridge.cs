using Unity.Netcode;
using UnityEngine;
using HutongGames.PlayMaker;

// Holds game-wide shared state (not per-player). Same bridge pattern as the
// player's health, but on an in-scene GameManager. Server writes, everyone reads.
public class GameStateBridge : NetworkBehaviour
{
    [UnityEngine.Tooltip("Fired to this object's FSMs when the GameManager is network-ready.")]
    public string SpawnEvent = "NETWORK_SPAWNED";

    [UnityEngine.Tooltip("Fired to this object's FSMs whenever the score changes.")]
    public string ScoreChangedEvent = "SCORE_CHANGED";

    private NetworkVariable<int> score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // For PlayMaker Get Property.
    public int ScoreValue => score.Value;

    // Call from any client (PlayMaker Call Method) to request adding score.
    public void RequestAddScore(int amount) => AddScoreRpc(amount);

    // PlayMaker-friendly, parameterless (sidesteps Call Method's int field/reset gotcha).
    public void AddOnePoint() => AddScoreRpc(1);

    // RequireOwnership = false is the key bit: the GameManager is owned by the SERVER,
    // so without this, non-host clients couldn't send this request.
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void AddScoreRpc(int amount)
    {
        score.Value += amount;
    }

    // Server-side direct (no RPC) — for server logic like pickup collection (Part F).
    public void ServerAddScore(int amount)
    {
        if (IsServer) score.Value += amount;
    }

    public override void OnNetworkSpawn()
    {
        score.OnValueChanged += HandleScoreChanged;
        SendEventToAllFsms(SpawnEvent);
        SendEventToAllFsms(ScoreChangedEvent); // show the starting value
    }

    public override void OnNetworkDespawn()
    {
        score.OnValueChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int previous, int current)
    {
        SendEventToAllFsms(ScoreChangedEvent);
    }

    private void SendEventToAllFsms(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return;
        PlayMakerFSM[] fsms = GetComponents<PlayMakerFSM>();
        foreach (PlayMakerFSM fsm in fsms)
        {
            fsm.SendEvent(eventName);
        }
    }
}