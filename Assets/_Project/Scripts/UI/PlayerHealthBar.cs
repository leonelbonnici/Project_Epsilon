using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    [UnityEngine.Tooltip("Image with Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    public Image fillImage;

    [UnityEngine.Tooltip("Root GameObject to enable/disable when toggling visibility.")]
    public GameObject visualRoot;

    [UnityEngine.Tooltip("If true, the bar hides when health is at full.")]
    public bool hideWhenFull = false;

    [UnityEngine.Tooltip("Optional color shift when health is critically low.")]
    public bool tintWhenLow = true;
    public float lowHealthThreshold = 0.3f;
    public Color normalColor = Color.green;
    public Color lowColor = Color.red;

    private NetworkPlayMakerBridge bridge;

    private void Awake()
    {
        bridge = GetComponentInParent<NetworkPlayMakerBridge>();
        if (visualRoot == null) visualRoot = gameObject;
    }

    private void LateUpdate()
    {
        if (bridge == null) return;

        float fraction = bridge.HealthNormalized;

        if (fillImage != null)
        {
            fillImage.fillAmount = fraction;
            if (tintWhenLow)
            {
                fillImage.color = fraction <= lowHealthThreshold ? lowColor : normalColor;
            }
        }

        if (hideWhenFull && visualRoot != null)
        {
            visualRoot.SetActive(fraction < 1f);
        }
    }
}