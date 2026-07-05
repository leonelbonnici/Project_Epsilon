using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ObjectivesHUD : MonoBehaviour
{
    [UnityEngine.Tooltip("Parent transform for quest entries. Vertical Layout Group recommended.")]
    public Transform entryRoot;

    [UnityEngine.Tooltip("Prefab used for each active quest. See structure notes.")]
    public GameObject questEntryPrefab;

    [UnityEngine.Tooltip("Maximum number of quests visible at once. Extras are hidden.")]
    public int maxVisibleQuests = 3;

    private QuestDatabase database;
    private bool wired = false;

    private void Update()
    {
        if (QuestJournalBridge.Instance == null) return;

        if (!wired)
        {
            QuestJournalBridge.Instance.Quests.OnListChanged += _ => Rebuild();
            database = QuestJournalBridge.Instance.database;
            wired = true;
            Rebuild();
        }
    }

    private void OnDisable()
    {
        wired = false;
    }

    private void Rebuild()
    {
        if (entryRoot == null || questEntryPrefab == null) return;
        if (QuestJournalBridge.Instance == null || database == null) return;

        foreach (Transform child in entryRoot) Destroy(child.gameObject);

        int shown = 0;
        foreach (var q in QuestJournalBridge.Instance.Quests)
        {
            if (q.state != QuestState.Active) continue;
            if (shown >= maxVisibleQuests) break;

            var def = database.GetById(q.questId.ToString());
            if (def == null) continue;

            var entry = Instantiate(questEntryPrefab, entryRoot);
            PopulateEntry(entry, def, q);
            shown++;
        }
    }

    private void PopulateEntry(GameObject entry, QuestDefinition def, QuestEntry runtime)
    {
        // Expect the prefab to have:
        //   - a top TMP_Text called "Title"
        //   - a child transform called "ObjectivesRoot" (a vertical layout)
        //   - an ObjectiveRow prefab (or we spawn TMP_Texts directly under ObjectivesRoot)

        var title = entry.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = def.displayTitle;

        var objRoot = entry.transform.Find("ObjectivesRoot");
        if (objRoot == null) return;

        foreach (Transform old in objRoot) Destroy(old.gameObject);

        for (int i = 0; i < def.objectives.Length; i++)
        {
            var obj = def.objectives[i];
            int current = runtime.GetProgress(i);
            int required = obj.requiredCount > 0 ? obj.requiredCount : 1;

            // Skip completed objectives per your "disappear when done" preference
            if (current >= required) continue;

            var line = new GameObject($"Objective_{i}");
            line.transform.SetParent(objRoot, false);
            var text = line.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = Color.white;
            text.text = obj.requiredCount > 0
                ? string.Format("· " + obj.displayText, current, required)
                : "· " + obj.displayText;
        }
    }
}