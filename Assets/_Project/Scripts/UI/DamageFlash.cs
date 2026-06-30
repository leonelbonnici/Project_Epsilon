using System.Collections;
using UnityEngine;

public class DamageFlash : MonoBehaviour
{
    [UnityEngine.Tooltip("Sprites to flash. If empty, auto-finds all SpriteRenderers in children at Awake.")]
    public SpriteRenderer[] targetSprites;

    [UnityEngine.Tooltip("Color to flash to.")]
    public Color flashColor = Color.white;

    [UnityEngine.Tooltip("How long the flash lasts (seconds).")]
    public float flashDuration = 0.1f;

    private Color[] originalColors;
    private bool colorsCaptured = false;
    private Coroutine activeFlash;

    private NetworkPlayMakerBridge playerBridge;
    private BossBridge bossBridge;

    private void Awake()
    {
        if (targetSprites == null || targetSprites.Length == 0)
        {
            targetSprites = GetComponentsInChildren<SpriteRenderer>(true);
        }

        playerBridge = GetComponent<NetworkPlayMakerBridge>();
        bossBridge = GetComponent<BossBridge>();
    }

    private void OnEnable()
    {
        if (playerBridge != null) playerBridge.HealthChanged += OnHealthChanged;
        if (bossBridge != null) bossBridge.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (playerBridge != null) playerBridge.HealthChanged -= OnHealthChanged;
        if (bossBridge != null) bossBridge.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float prev, float curr)
    {
        // Skip flash on the killing blow — downed visual takes over instead.
        if (curr < prev && curr > 0f) Flash();
    }

    public void Flash()
    {
        // Lazy capture: grab originals at the moment of the first flash, by which point
        // any spawn-time color assignment (e.g., per-player tint FSM) has run.
        if (!colorsCaptured) CaptureOriginalColors();

        if (activeFlash != null) StopCoroutine(activeFlash);
        activeFlash = StartCoroutine(FlashRoutine());
    }

    private void CaptureOriginalColors()
    {
        originalColors = new Color[targetSprites.Length];
        for (int i = 0; i < targetSprites.Length; i++)
        {
            if (targetSprites[i] != null) originalColors[i] = targetSprites[i].color;
        }
        colorsCaptured = true;
    }

    private IEnumerator FlashRoutine()
    {
        SetColor(flashColor);
        yield return new WaitForSeconds(flashDuration);
        RestoreColors();
        activeFlash = null;
    }

    private void SetColor(Color c)
    {
        foreach (var sr in targetSprites)
        {
            if (sr != null) sr.color = c;
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < targetSprites.Length; i++)
        {
            if (targetSprites[i] != null) targetSprites[i].color = originalColors[i];
        }
    }
}