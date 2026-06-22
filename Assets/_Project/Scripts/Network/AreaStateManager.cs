using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AreaStateManager : MonoBehaviour
{
    public static AreaStateManager Instance { get; private set; }

    private const string SAVE_KEY = "AreaStateManager.AreaStates";

    private Dictionary<string, Dictionary<string, string>> areaStates
        = new Dictionary<string, Dictionary<string, string>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadFromDisk();
    }

    /// <summary>
    /// Server-only: walks the given scene's objects, captures state from all IPersistables,
    /// stores under the area name. Replaces any prior snapshot for that area.
    /// </summary>
    public void SnapshotArea(string areaName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (string.IsNullOrEmpty(areaName)) return;

        Scene scene = SceneManager.GetSceneByName(areaName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"[AreaStateManager] Cannot snapshot '{areaName}' — scene not loaded.");
            return;
        }

        var snapshot = new Dictionary<string, string>();
        foreach (var p in FindPersistablesInScene(scene))
        {
            if (string.IsNullOrEmpty(p.PersistenceId)) continue;
            snapshot[p.PersistenceId] = p.CaptureState();
        }

        areaStates[areaName] = snapshot;
        Debug.Log($"[AreaStateManager] Snapshotted {snapshot.Count} objects in '{areaName}'.");

        SaveToDisk();
    }

    /// <summary>
    /// Server-only: walks the given scene's objects, restores state on any IPersistable
    /// whose ID matches a prior snapshot. Silently no-ops if no snapshot exists.
    /// </summary>
    public void RestoreArea(string areaName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (string.IsNullOrEmpty(areaName)) return;
        if (!areaStates.TryGetValue(areaName, out var snapshot)) return;

        Scene scene = SceneManager.GetSceneByName(areaName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"[AreaStateManager] Cannot restore '{areaName}' — scene not loaded.");
            return;
        }

        int restored = 0;
        foreach (var p in FindPersistablesInScene(scene))
        {
            if (string.IsNullOrEmpty(p.PersistenceId)) continue;
            if (snapshot.TryGetValue(p.PersistenceId, out var state))
            {
                p.RestoreState(state);
                restored++;
            }
        }
        Debug.Log($"[AreaStateManager] Restored {restored} objects in '{areaName}'.");

        // Trigger door re-evaluation since restored room states didn't fire RoomCompleted events
        foreach (var root in scene.GetRootGameObjects())
        {
            var ab = root.GetComponentInChildren<AreaBridge>(true);
            if (ab != null)
            {
                ab.ServerEvaluateAllDoors();
                break;
            }
        }
    }

    /// <summary>
    /// Wipes all in-memory and on-disk area state. Useful for "new game" buttons and dev testing.
    /// </summary>
    public void ClearAllSavedState()
    {
        areaStates.Clear();
        try
        {
            if (ES3.KeyExists(SAVE_KEY)) ES3.DeleteKey(SAVE_KEY);
            Debug.Log("[AreaStateManager] Cleared all in-memory and on-disk state.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AreaStateManager] Failed to clear save data: {e.Message}");
        }
    }

    private void SaveToDisk()
    {
        try
        {
            ES3.Save(SAVE_KEY, areaStates);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AreaStateManager] Failed to save to disk: {e.Message}");
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!ES3.KeyExists(SAVE_KEY)) return;

            var loaded = ES3.Load<Dictionary<string, Dictionary<string, string>>>(SAVE_KEY);
            if (loaded != null)
            {
                areaStates = loaded;
                Debug.Log($"[AreaStateManager] Loaded state for {areaStates.Count} area(s) from disk.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AreaStateManager] Failed to load from disk: {e.Message}");
            areaStates = new Dictionary<string, Dictionary<string, string>>();
        }
    }

    private List<IPersistable> FindPersistablesInScene(Scene scene)
    {
        var list = new List<IPersistable>();
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            var components = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var c in components)
            {
                if (c is IPersistable p) list.Add(p);
            }
        }
        return list;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/AreaState/Clear Save Data")]
    private static void EditorClearSaveData()
    {
        try
        {
            if (ES3.KeyExists(SAVE_KEY))
            {
                ES3.DeleteKey(SAVE_KEY);
                Debug.Log("[AreaStateManager] Cleared save data via editor menu.");
            }
            else
            {
                Debug.Log("[AreaStateManager] No save data found.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AreaStateManager] Failed to clear save data: {e.Message}");
        }
    }
#endif
}