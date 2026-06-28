using UnityEngine;
using UnityEngine.UI;

public class BossHealthHUD : MonoBehaviour
{
    [UnityEngine.Tooltip("Image with Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    public Image fillImage;

    [UnityEngine.Tooltip("Optional: text component showing the boss's display name.")]
    public TMPro.TMP_Text nameLabel;

    [UnityEngine.Tooltip("Root GameObject to enable/disable for showing/hiding the bar. Defaults to this GameObject.")]
    public GameObject visualRoot;

    [UnityEngine.Tooltip("How often (seconds) to look for an active boss in the scene.")]
    public float pollInterval = 0.3f;

    private BossBridge currentBoss;
    private float pollTimer = 0f;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = gameObject;
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    private void Update()
    {
        pollTimer += Time.deltaTime;
        if (pollTimer >= pollInterval)
        {
            pollTimer = 0f;
            RefreshBoss();
        }

        UpdateBar();
    }

    private void RefreshBoss()
    {
        // Drop stale reference
        if (currentBoss != null && (currentBoss.gameObject == null || !currentBoss.gameObject.activeInHierarchy))
        {
            currentBoss = null;
        }

        // If we already have a healthy boss, keep it
        if (currentBoss != null && currentBoss.HealthValue > 0f) return;

        currentBoss = null;
        var all = Object.FindObjectsByType<BossBridge>(FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.HealthValue > 0f)
            {
                currentBoss = b;
                if (nameLabel != null)
                {
                    nameLabel.text = string.IsNullOrEmpty(b.displayName) ? b.gameObject.name : b.displayName;
                }
                break;
            }
        }
    }

    private void UpdateBar()
    {
        bool show = currentBoss != null && currentBoss.HealthValue > 0f;

        if (visualRoot != null && visualRoot.activeSelf != show)
        {
            visualRoot.SetActive(show);
        }

        if (show && fillImage != null)
        {
            //Debug.Log($"[BossHUD] {currentBoss.gameObject.name} HP {currentBoss.HealthValue}/{currentBoss.maxHealth} = {currentBoss.HealthNormalized}");
            fillImage.fillAmount = currentBoss.HealthNormalized;
        }
    }
}