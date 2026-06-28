using UnityEngine;
using UnityEngine.UI;

public class PartyMemberSlot : MonoBehaviour
{
    [UnityEngine.Tooltip("Image with Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    public Image fillImage;

    [UnityEngine.Tooltip("Optional: GameObject shown only when this slot represents the local player.")]
    public GameObject selfHighlight;

    [UnityEngine.Tooltip("Optional: GameObject to enable/disable for showing/hiding the slot. Defaults to this GameObject.")]
    public GameObject visualRoot;

    public Color normalColor = Color.green;
    public Color lowColor = Color.red;
    public float lowHealthThreshold = 0.3f;

    private NetworkPlayMakerBridge bridge;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = gameObject;
    }

    public void SetBridge(NetworkPlayMakerBridge b, bool isSelf)
    {
        bridge = b;
        if (visualRoot != null) visualRoot.SetActive(b != null);
        if (selfHighlight != null) selfHighlight.SetActive(b != null && isSelf);
    }

    private void Update()
    {
        if (bridge == null || fillImage == null) return;

        float frac = bridge.HealthNormalized;
        fillImage.fillAmount = frac;
        fillImage.color = frac <= lowHealthThreshold ? lowColor : normalColor;
    }
}