using UnityEngine;

[CreateAssetMenu(fileName = "QuestDatabase", menuName = "Game/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    public QuestDefinition[] allQuests;

    public QuestDefinition GetById(string id)
    {
        if (allQuests == null) return null;
        foreach (var q in allQuests)
            if (q != null && q.questId == id) return q;
        return null;
    }
}