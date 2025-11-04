using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SkillInputTest : MonoBehaviour
{
    public SkillRunner runner;
    public SkillProfile charge, roll, blink;
    public SkillGate gate;
    void Update()
    {
        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) TryUse(charge);
        if (kb.digit2Key.wasPressedThisFrame) TryUse(roll);
        if (kb.digit3Key.wasPressedThisFrame) TryUse(blink);
        #else
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryUse(charge);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryUse(roll);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryUse(blink);
        #endif
    }

    void TryUse(SkillProfile s)
    {
        Debug.Log($"[Cast] {s.displayName} kind={s.kind} move={s.moveDistance}");
        if (gate.CanUse(s)) runner.RunSkill(s);
        // Optional: simple gate before calling runner.RunSkill(s)
        // runner.RunSkill(s);
    }
}
