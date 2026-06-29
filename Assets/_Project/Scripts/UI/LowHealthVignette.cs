using MoreMountains.Feedbacks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LowHealthVignette : MonoBehaviour
{
    [UnityEngine.Tooltip("Image used as the vignette overlay. Should stretch fullscreen, anchored to all corners.")]
    public Image vignetteImage;

    [UnityEngine.Tooltip("Health fraction (0-1) at which the vignette starts to appear.")]
    [Range(0f, 1f)] public float threshold = 0.4f;

    [UnityEngine.Tooltip("Maximum alpha the vignette reaches at 0 HP.")]
    [Range(0f, 1f)] public float maxAlpha = 0.7f;

    [UnityEngine.Tooltip("How often (seconds) to look for the local player's bridge.")]
    public float pollInterval = 0.5f;

    [UnityEngine.Tooltip("Optional Feel feedback played when crossing into low-health state (e.g., start heartbeat sound).")]
    public MMF_Player onEnterLowHealth;
    [UnityEngine.Tooltip("Optional Feel feedback played when crossing back above the threshold (e.g., stop heartbeat sound).")]
    public MMF_Player onExitLowHealth;

    private NetworkPlayMakerBridge localBridge;
    private float pollTimer;
    private bool currentlyLow = false;

    private void Start()
    {
        SetVignetteAlpha(0f);
    }

    private void Update()
    {
        pollTimer += Time.deltaTime;
        if (pollTimer >= pollInterval || localBridge == null)
        {
            pollTimer = 0f;
            RefreshLocalBridge();
        }

        if (localBridge == null)
        {
            SetVignetteAlpha(0f);
            if (currentlyLow)
            {
                currentlyLow = false;
                if (onExitLowHealth != null) onExitLowHealth.PlayFeedbacks();
            }
            return;
        }

        float hp = localBridge.HealthNormalized;
        bool isLow = hp < threshold;
        float intensity = isLow ? Mathf.Clamp01((threshold - hp) / threshold) : 0f;
        SetVignetteAlpha(intensity * maxAlpha);

        if (isLow && !currentlyLow)
        {
            currentlyLow = true;
            if (onEnterLowHealth != null) onEnterLowHealth.PlayFeedbacks();
        }
        else if (!isLow && currentlyLow)
        {
            currentlyLow = false;
            if (onExitLowHealth != null) onExitLowHealth.PlayFeedbacks();
        }
    }

    private void SetVignetteAlpha(float a)
    {
        if (vignetteImage == null) return;
        var c = vignetteImage.color;
        c.a = a;
        vignetteImage.color = c;
    }

    private void RefreshLocalBridge()
    {
        if (NetworkManager.Singleton == null) return;
        var local = NetworkManager.Singleton.LocalClient;
        if (local == null || local.PlayerObject == null) return;
        localBridge = local.PlayerObject.GetComponent<NetworkPlayMakerBridge>();
    }
}