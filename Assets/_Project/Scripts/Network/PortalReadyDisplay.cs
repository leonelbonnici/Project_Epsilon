using TMPro;
using UnityEngine;

public class PortalReadyDisplay : MonoBehaviour
{
    public ScenePortal portal;
    public TMP_Text text;
    public float updateInterval = 0.2f;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        if (portal != null && text != null)
            text.text = $"{portal.ReadyCount}/{portal.TotalPlayers} Ready";
    }
}