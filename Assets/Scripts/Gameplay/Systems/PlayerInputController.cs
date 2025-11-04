using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInputController : MonoBehaviour
{
    [SerializeField] private PlayerSpaceCoordinator coordinator;
    [SerializeField] private float moveSpeed = 3.0f;   // units per second
    [SerializeField] private float fineFactor = 0.4f;  // hold Shift to move slower

    void Update()
    {
        float axis = 0f;

        // New Input System
        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed)  axis -= 1f;
            if (kb.rightArrowKey.isPressed) axis += 1f;

            float speed = moveSpeed * ((kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) ? fineFactor : 1f);
            if (Mathf.Abs(axis) > 0f)
                coordinator.MoveByInput(axis, speed);

            // Tiny nudge test
            if (kb.zKey.wasPressedThisFrame) coordinator.RequestPlayerDisplacement(-0.05f);
            if (kb.xKey.wasPressedThisFrame) coordinator.RequestPlayerDisplacement(+0.05f);
        }
        #else
        // Old Input Manager fallback
        if (Input.GetKey(KeyCode.LeftArrow))  axis -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) axis += 1f;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fineFactor : 1f);
        if (Mathf.Abs(axis) > 0f)
            coordinator.MoveByInput(axis, speed);

        if (Input.GetKeyDown(KeyCode.Z)) coordinator.RequestPlayerDisplacement(-0.05f);
        if (Input.GetKeyDown(KeyCode.X)) coordinator.RequestPlayerDisplacement(+0.05f);
        #endif
    }
}
