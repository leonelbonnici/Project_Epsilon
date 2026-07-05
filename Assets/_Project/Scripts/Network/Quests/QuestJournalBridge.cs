using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum QuestState : byte
{
    Unstarted = 0,
    Active = 1,
    Complete = 2,
    Failed = 3
}

// Fixed-size struct — we can't have variable-length data inside a NetworkList element.
// Preallocate 4 objective counters per quest. Most quests use 0-1.
public struct QuestEntry : INetworkSerializable, System.IEquatable<QuestEntry>
{
    public FixedString32Bytes questId;
    public QuestState state;
    public int progress0;
    public int progress1;
    public int progress2;
    public int progress3;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref questId);
        s.SerializeValue(ref state);
        s.SerializeValue(ref progress0);
        s.SerializeValue(ref progress1);
        s.SerializeValue(ref progress2);
        s.SerializeValue(ref progress3);
    }

    public bool Equals(QuestEntry other) => questId.Equals(other.questId);

    public int GetProgress(int objectiveIndex)
    {
        switch (objectiveIndex)
        {
            case 0: return progress0;
            case 1: return progress1;
            case 2: return progress2;
            case 3: return progress3;
            default: return 0;
        }
    }

    public void SetProgress(int objectiveIndex, int value)
    {
        switch (objectiveIndex)
        {
            case 0: progress0 = value; break;
            case 1: progress1 = value; break;
            case 2: progress2 = value; break;
            case 3: progress3 = value; break;
        }
    }
}

public class QuestJournalBridge : NetworkBehaviour, IPersistable
{
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestStartQuestRpc(string questId) => ServerStartQuest(questId);

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestAdvanceObjectiveRpc(string questId, int objectiveIndex, int delta) 
        => ServerAdvanceObjective(questId, objectiveIndex, delta);

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestCompleteObjectiveRpc(string questId, int objectiveIndex) 
        => ServerCompleteObjective(questId, objectiveIndex);

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void RequestCompleteQuestRpc(string questId) => ServerCompleteQuest(questId);
    
    public static QuestJournalBridge Instance { get; private set; }

    public NetworkList<QuestEntry> Quests;

    [UnityEngine.Tooltip("Database of quest definitions — display titles and objectives. Assign the QuestDatabase asset.")]
    public QuestDatabase database;

    public string PersistenceId => "quest_journal";

    private void Awake()
    {
        Quests = new NetworkList<QuestEntry>();
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Server-side mutations ---

    public void ServerStartQuest(string questId)
    {
        if (!IsServer) return;
        if (string.IsNullOrEmpty(questId)) return;

        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            if (Quests[i].state == QuestState.Unstarted)
            {
                var q = Quests[i];
                q.state = QuestState.Active;
                Quests.RemoveAt(i);
                Quests.Insert(i, q);
            }
            return; // already exists in some state, don't restart
        }

        Quests.Add(new QuestEntry
        {
            questId = questId,
            state = QuestState.Active,
            progress0 = 0, progress1 = 0, progress2 = 0, progress3 = 0
        });
    }

    public void ServerAdvanceObjective(string questId, int objectiveIndex, int delta = 1)
    {
        if (!IsServer) return;
        if (string.IsNullOrEmpty(questId)) return;
        if (objectiveIndex < 0 || objectiveIndex > 3) return;

        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            if (Quests[i].state != QuestState.Active) return;

            var q = Quests[i];
            q.SetProgress(objectiveIndex, q.GetProgress(objectiveIndex) + delta);
            Quests.RemoveAt(i);
            Quests.Insert(i, q);

            CheckQuestComplete(questId);
            return;
        }
    }

    public void ServerCompleteObjective(string questId, int objectiveIndex)
    {
        if (!IsServer) return;
        if (database == null) return;

        var def = database.GetById(questId);
        if (def == null || objectiveIndex < 0 || objectiveIndex >= def.objectives.Length) return;

        var target = def.objectives[objectiveIndex].requiredCount;
        if (target <= 0) target = 1; // binary objectives count as 1

        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            if (Quests[i].state != QuestState.Active) return;

            var q = Quests[i];
            q.SetProgress(objectiveIndex, target);
            Quests.RemoveAt(i);
            Quests.Insert(i, q);

            CheckQuestComplete(questId);
            return;
        }
    }

    public void ServerCompleteQuest(string questId)
    {
        if (!IsServer) return;
        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            var q = Quests[i];
            q.state = QuestState.Complete;
            Quests.RemoveAt(i);
            Quests.Insert(i, q);
            return;
        }
    }

    public void ServerFailQuest(string questId)
    {
        if (!IsServer) return;
        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            var q = Quests[i];
            q.state = QuestState.Failed;
            Quests.RemoveAt(i);
            Quests.Insert(i, q);
            return;
        }
    }

    private void CheckQuestComplete(string questId)
    {
        if (database == null) return;
        var def = database.GetById(questId);
        if (def == null) return;

        for (int i = 0; i < Quests.Count; i++)
        {
            if (Quests[i].questId.ToString() != questId) continue;
            var q = Quests[i];
            if (q.state != QuestState.Active) return;

            for (int obj = 0; obj < def.objectives.Length; obj++)
            {
                int target = def.objectives[obj].requiredCount;
                if (target <= 0) target = 1;
                if (q.GetProgress(obj) < target) return; // still work to do
            }

            // All objectives done
            ServerCompleteQuest(questId);
            return;
        }
    }

    // --- Client-safe reads ---

    public QuestState GetState(string questId)
    {
        if (Quests == null) return QuestState.Unstarted;
        foreach (var q in Quests)
            if (q.questId.ToString() == questId) return q.state;
        return QuestState.Unstarted;
    }

    public int GetProgress(string questId, int objectiveIndex)
    {
        if (Quests == null) return 0;
        foreach (var q in Quests)
            if (q.questId.ToString() == questId) return q.GetProgress(objectiveIndex);
        return 0;
    }

    // --- IPersistable ---

    public string CaptureState()
    {
        if (Quests == null || Quests.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var q in Quests)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(q.questId).Append(':')
              .Append((int)q.state).Append(':')
              .Append(q.progress0).Append(',')
              .Append(q.progress1).Append(',')
              .Append(q.progress2).Append(',')
              .Append(q.progress3);
        }
        return sb.ToString();
    }

    public void RestoreState(string state)
    {
        if (!IsServer) return;
        Quests.Clear();
        if (string.IsNullOrEmpty(state)) return;

        foreach (var entry in state.Split('|'))
        {
            var parts = entry.Split(':');
            if (parts.Length != 3) continue;
            if (!int.TryParse(parts[1], out int stateInt)) continue;

            var progressParts = parts[2].Split(',');
            if (progressParts.Length != 4) continue;

            var q = new QuestEntry
            {
                questId = parts[0],
                state = (QuestState)stateInt
            };
            if (int.TryParse(progressParts[0], out int p0)) q.progress0 = p0;
            if (int.TryParse(progressParts[1], out int p1)) q.progress1 = p1;
            if (int.TryParse(progressParts[2], out int p2)) q.progress2 = p2;
            if (int.TryParse(progressParts[3], out int p3)) q.progress3 = p3;
            Quests.Add(q);
        }
    }
}