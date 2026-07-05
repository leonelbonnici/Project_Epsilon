using UnityEngine;

[CreateAssetMenu(fileName = "Quest_", menuName = "Game/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    [UnityEngine.Tooltip("Must match the runtime quest ID used in code and Dialogue System.")]
    public string questId;

    [UnityEngine.Tooltip("Shown as the quest header in the objectives HUD.")]
    public string displayTitle;

    public QuestObjective[] objectives;
}

[System.Serializable]
public class QuestObjective
{
    [UnityEngine.Tooltip("Text shown in the HUD. Use {0} for current progress, {1} for required count.")]
    [TextArea(1, 3)]
    public string displayText;

    [UnityEngine.Tooltip("Target count for this objective. 0 = binary. >0 = counter.")]
    public int requiredCount = 0;

    [UnityEngine.Tooltip("Indices of objectives that must be complete before this one is revealed. Leave empty to always show.")]
    public int[] prerequisiteIndices;
}