using Unity.Netcode;
using UnityEngine;

// Follows the local client's player. Each machine has its own camera that locks onto
// its own player object via NGO's LocalClient reference.
public class CameraFollowLocalPlayer : MonoBehaviour
{
    [UnityEngine.Tooltip("How smoothly the camera tracks the player. 0 = snap, higher = smoother.")]
    public float smoothing = 0f;

    [UnityEngine.Tooltip("Z offset for the camera (typically -10 for 2D).")]
    public float zOffset = -10f;

    [UnityEngine.Tooltip("How often (seconds) to re-search for the local player if not found yet.")]
    public float searchInterval = 0.3f;

    private Transform target;
    private float searchTimer;

    private void LateUpdate()
    {
        if (target == null)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchInterval)
            {
                searchTimer = 0f;
                target = FindLocalPlayer();
            }
            if (target == null) return;
        }

        Vector3 desired = new Vector3(target.position.x, target.position.y, zOffset);
        if (smoothing <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime / smoothing);
    }

    private Transform FindLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return null;
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return null;
        return localClient.PlayerObject.transform;
    }
}