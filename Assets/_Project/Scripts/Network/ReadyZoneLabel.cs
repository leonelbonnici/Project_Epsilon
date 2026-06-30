using TMPro;
using UnityEngine;

// Drop on the same object as a ReadyZone (or any object that references one) and
// assign a TMP label. Shows "X/Y" so players can see who's still missing.
public class ReadyZoneLabel : MonoBehaviour
{
    [UnityEngine.Tooltip("ReadyZone to read from. If null, uses GetComponent on this object.")]
    public ReadyZone zone;

    [UnityEngine.Tooltip("Text element to write the count into (world-space canvas recommended).")]
    public TMP_Text label;

    [UnityEngine.Tooltip("Format string. {0} = ready count, {1} = total count.")]
    public string format = "{0}/{1}";

    private void Awake()
    {
        if (zone == null) zone = GetComponent<ReadyZone>();
    }

    private void Update()
    {
        if (zone == null || label == null) return;
        label.text = string.Format(format, zone.ReadyCount, zone.TotalCount);
    }
}