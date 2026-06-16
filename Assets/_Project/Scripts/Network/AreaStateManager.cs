using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AreaStateManager : MonoBehaviour
{
    public static AreaStateManager Instance { get; private set; }

    private readonly Dictionary<string, Dictionary<string, string>> areaStates
        = new Dictionary<string, Dictionary<string, string>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
    }

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
}