using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// IRoom that completes when switches reach a target configuration. Two modes:
//   - Pattern mode: requirements list specifies a per-switch state (any order).
//   - Sequence mode: requiredSequence specifies an ORDER of switch activations.
// If requiredSequence has any entries, sequence mode is used and requirements is ignored.
public class PuzzleRoom : NetworkBehaviour, IRoom
{
    [System.Serializable]
    public class SwitchRequirement
    {
        public string switchId;
        public bool requiredState = true;
    }

    [UnityEngine.Tooltip("Unique identifier within the area.")]
    public string roomId = "puzzle_room_01";

    [UnityEngine.Tooltip("PATTERN MODE: switches and the state each must be in. Ignored if Required Sequence is populated.")]
    public List<SwitchRequirement> requirements = new List<SwitchRequirement>();

    [UnityEngine.Tooltip("SEQUENCE MODE: switch IDs in the order they must be activated. Set this for ordered puzzles; leave empty for pattern mode.")]
    public List<string> requiredSequence = new List<string>();

    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private List<PuzzleSwitch> trackedSwitches = new List<PuzzleSwitch>();
    private int currentSequenceIndex = 0;
    private bool isResetting = false;        // guard against re-entry during sequence reset

    public string RoomId => roomId;
    public bool IsCompleted => isCompleted.Value;
    public event System.Action<IRoom> RoomCompleted;

    public override void OnNetworkSpawn()
    {
        if (IsServer) SubscribeToSceneSwitches();
    }

    public override void OnNetworkDespawn()
    {
        foreach (var sw in trackedSwitches)
            if (sw != null) sw.StateChanged -= OnSwitchChanged;
        trackedSwitches.Clear();
    }

    private void SubscribeToSceneSwitches()
    {
        HashSet<string> trackIds = new HashSet<string>();
        if (requiredSequence.Count > 0)
            foreach (var id in requiredSequence) trackIds.Add(id);
        else
            foreach (var req in requirements) trackIds.Add(req.switchId);

        foreach (var sw in FindObjectsByType<PuzzleSwitch>(FindObjectsSortMode.None))
        {
            if (trackIds.Contains(sw.SwitchId))
            {
                sw.StateChanged += OnSwitchChanged;
                trackedSwitches.Add(sw);
            }
        }
    }

    private void OnSwitchChanged(PuzzleSwitch sw)
    {
        if (!IsServer) return;
        if (isCompleted.Value) return;
        if (isResetting) return;             // skip events fired by our own reset loop

        if (requiredSequence.Count > 0) EvaluateSequence(sw);
        else EvaluatePattern();
    }

    private void EvaluateSequence(PuzzleSwitch sw)
    {
        // We only care about activations in sequence mode (deactivations only happen during reset).
        if (!sw.IsActive) return;

        string expectedId = requiredSequence[currentSequenceIndex];
        if (sw.SwitchId == expectedId)
        {
            currentSequenceIndex++;
            if (currentSequenceIndex >= requiredSequence.Count)
            {
                isCompleted.Value = true;
                RoomCompleted?.Invoke(this);
            }
        }
        else
        {
            ResetSequence();
        }
    }

    private void ResetSequence()
    {
        isResetting = true;
        foreach (var sw in trackedSwitches)
            if (sw != null) sw.ServerSetActive(false);
        isResetting = false;
        currentSequenceIndex = 0;
    }

    private void EvaluatePattern()
    {
        foreach (var req in requirements)
        {
            PuzzleSwitch found = trackedSwitches.Find(s => s.SwitchId == req.switchId);
            if (found == null || found.IsActive != req.requiredState) return;
        }
        isCompleted.Value = true;
        RoomCompleted?.Invoke(this);
    }
}