using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PartyHealthHUD : MonoBehaviour
{
    [UnityEngine.Tooltip("Slots in display order. Slot 0 always represents the local player.")]
    public PartyMemberSlot[] slots;

    [UnityEngine.Tooltip("How often (seconds) to refresh slot-to-bridge assignments. Health values update every frame regardless.")]
    public float refreshInterval = 0.5f;

    private float refreshTimer = 0f;
    private List<NetworkPlayMakerBridge> orderedBridges = new List<NetworkPlayMakerBridge>();

    private void Start()
    {
        RefreshAndAssign();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshAndAssign();
        }
    }

    private void RefreshAndAssign()
    {
        orderedBridges.Clear();

        var localBridge = GetLocalBridge();
        if (localBridge != null) orderedBridges.Add(localBridge);

        // Find all player bridges in the scene, sort for stable ordering, add non-local ones.
        var allBridges = Object.FindObjectsByType<NetworkPlayMakerBridge>(FindObjectsSortMode.None);
        System.Array.Sort(allBridges, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        foreach (var b in allBridges)
        {
            if (b == localBridge) continue;
            orderedBridges.Add(b);
        }

        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < orderedBridges.Count)
            {
                slots[i].SetBridge(orderedBridges[i], isSelf: i == 0);
            }
            else
            {
                slots[i].SetBridge(null, isSelf: false);
            }
        }
    }

    private NetworkPlayMakerBridge GetLocalBridge()
    {
        if (NetworkManager.Singleton == null) return null;
        var local = NetworkManager.Singleton.LocalClient;
        if (local == null || local.PlayerObject == null) return null;
        return local.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
    }
}