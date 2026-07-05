using PixelCrushers.DialogueSystem;
using UnityEngine;

// Registers custom Lua functions used by Dialogue System conversations to query
// and modify the quest journal and shared inventory. Put this on a persistent
// GameObject that the Dialogue Manager will find at scene start.
public class DialogueSystemLuaBindings : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log("[LuaBindings] OnEnable - registering Lua functions");

        Lua.RegisterFunction("QuestState", this, SymbolExtensions.GetMethodInfo(() => QuestState(string.Empty)));
        Lua.RegisterFunction("ObjectiveProgress", this, SymbolExtensions.GetMethodInfo(() => ObjectiveProgress(string.Empty, (double)0)));
        Lua.RegisterFunction("StartQuest", this, SymbolExtensions.GetMethodInfo(() => StartQuest(string.Empty)));
        Lua.RegisterFunction("AdvanceObjective", this, SymbolExtensions.GetMethodInfo(() => AdvanceObjective(string.Empty, (double)0, (double)0)));
        Lua.RegisterFunction("CompleteObjective", this, SymbolExtensions.GetMethodInfo(() => CompleteObjective(string.Empty, (double)0)));
        Lua.RegisterFunction("CompleteQuest", this, SymbolExtensions.GetMethodInfo(() => CompleteQuest(string.Empty)));
        Lua.RegisterFunction("HasItem", this, SymbolExtensions.GetMethodInfo(() => HasItem(string.Empty, (double)0)));
        Lua.RegisterFunction("AddItem", this, SymbolExtensions.GetMethodInfo(() => AddItem(string.Empty, (double)0)));
        Lua.RegisterFunction("RemoveItem", this, SymbolExtensions.GetMethodInfo(() => RemoveItem(string.Empty, (double)0)));

        Debug.Log("[LuaBindings] OnEnable - all functions registered");
    }

    private void OnDisable()
    {
        Lua.UnregisterFunction("QuestState");
        Lua.UnregisterFunction("ObjectiveProgress");
        Lua.UnregisterFunction("StartQuest");
        Lua.UnregisterFunction("AdvanceObjective");
        Lua.UnregisterFunction("CompleteObjective");
        Lua.UnregisterFunction("CompleteQuest");
        Lua.UnregisterFunction("HasItem");
        Lua.UnregisterFunction("AddItem");
        Lua.UnregisterFunction("RemoveItem");
    }

    // --- Query bindings (safe on any client) ---

    public string QuestState(string questId)
    {
        var result = QuestJournalBridge.Instance == null ? "Unstarted" : QuestJournalBridge.Instance.GetState(questId).ToString();
        Debug.Log($"[LuaBindings] QuestState('{questId}') -> {result}");
        return result;
    }

    public double ObjectiveProgress(string questId, double objectiveIndex)
    {
        if (QuestJournalBridge.Instance == null) return 0;
        return QuestJournalBridge.Instance.GetProgress(questId, (int)objectiveIndex);
    }

    public bool HasItem(string itemId, double count)
    {
        if (SharedInventoryBridge.Instance == null) return false;
        return SharedInventoryBridge.Instance.HasItem(itemId, (int)count);
    }

    public void StartQuest(string questId)
    {
        if (QuestJournalBridge.Instance == null) return;
        QuestJournalBridge.Instance.RequestStartQuestRpc(questId);
    }

    public void AdvanceObjective(string questId, double objectiveIndex, double delta)
    {
        if (QuestJournalBridge.Instance == null) return;
        QuestJournalBridge.Instance.RequestAdvanceObjectiveRpc(questId, (int)objectiveIndex, (int)delta);
    }

    public void CompleteObjective(string questId, double objectiveIndex)
    {
        if (QuestJournalBridge.Instance == null) return;
        QuestJournalBridge.Instance.RequestCompleteObjectiveRpc(questId, (int)objectiveIndex);
    }

    public void CompleteQuest(string questId)
    {
        if (QuestJournalBridge.Instance == null) return;
        QuestJournalBridge.Instance.RequestCompleteQuestRpc(questId);
    }

    public void AddItem(string itemId, double count)
    {
        if (SharedInventoryBridge.Instance == null) return;
        SharedInventoryBridge.Instance.RequestAddItemRpc(itemId, (int)count);
    }

    public void RemoveItem(string itemId, double count)
    {
        if (SharedInventoryBridge.Instance == null) return;
        SharedInventoryBridge.Instance.RequestRemoveItemRpc(itemId, (int)count);
    }
}