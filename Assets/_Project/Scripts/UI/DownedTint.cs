using UnityEngine;

public class DownedTint : MonoBehaviour
{
    [UnityEngine.Tooltip("Sprites to tint. If empty, auto-finds all SpriteRenderers in children.")]
    public SpriteRenderer[] targetSprites;

    [UnityEngine.Tooltip("Color applied while downed (grey by default).")]
    public Color downedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private Color[] cachedColors;
    private NetworkPlayMakerBridge bridge;

    private void Awake()
    {
        if (targetSprites == null || targetSprites.Length == 0)
        {
            targetSprites = GetComponentsInChildren<SpriteRenderer>(true);
        }
        bridge = GetComponent<NetworkPlayMakerBridge>();
    }

    private void OnEnable()
    {
        if (bridge != null) bridge.DownedChanged += OnDownedChanged;
    }

    private void OnDisable()
    {
        if (bridge != null) bridge.DownedChanged -= OnDownedChanged;
    }

    private void OnDownedChanged(bool isDowned)
    {
        if (isDowned) CacheAndApplyDowned();
        else RestoreFromCache();
    }

    private void CacheAndApplyDowned()
    {
        cachedColors = new Color[targetSprites.Length];
        for (int i = 0; i < targetSprites.Length; i++)
        {
            if (targetSprites[i] != null)
            {
                cachedColors[i] = targetSprites[i].color;
                targetSprites[i].color = downedColor;
            }
        }
    }

    private void RestoreFromCache()
    {
        if (cachedColors == null) return;
        for (int i = 0; i < targetSprites.Length && i < cachedColors.Length; i++)
        {
            if (targetSprites[i] != null)
            {
                targetSprites[i].color = cachedColors[i];
            }
        }
    }
}