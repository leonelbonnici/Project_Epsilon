using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 2D client-authoritative movement with diagonal support.
/// WASD moves the player on the XY plane.
/// </summary>
public class ClientAuthoritativeMovement2D : NetworkBehaviour
{
    public float Speed = 5;

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        var multiplier = Speed * Time.deltaTime;
        Vector3 move = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current.aKey.isPressed) move.x -= 1;
        if (Keyboard.current.dKey.isPressed) move.x += 1;
        if (Keyboard.current.wKey.isPressed) move.y += 1;
        if (Keyboard.current.sKey.isPressed) move.y -= 1;
#else
        if (Input.GetKey(KeyCode.A)) move.x -= 1;
        if (Input.GetKey(KeyCode.D)) move.x += 1;
        if (Input.GetKey(KeyCode.W)) move.y += 1;
        if (Input.GetKey(KeyCode.S)) move.y -= 1;
#endif

        transform.position += move.normalized * multiplier;
    }
}