using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SkillInputTest : MonoBehaviour
{
    public SkillRunner runner;
    public SkillGate gate;

    // Displacement
    public SkillProfile charge;   // DashToward
    public SkillProfile roll;     // DashAway
    public SkillProfile blink;    // BlinkAway

    // Damage
    public SkillProfile conflagrate; // Mage
    public SkillProfile snipe;       // Archer
    public SkillProfile smash;       // Warrior

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) TryUse(charge);
        if (kb.digit2Key.wasPressedThisFrame) TryUse(roll);
        if (kb.digit3Key.wasPressedThisFrame) TryUse(blink);
        if (kb.digit4Key.wasPressedThisFrame) TryUse(conflagrate);
        if (kb.digit5Key.wasPressedThisFrame) TryUse(snipe);
        if (kb.digit6Key.wasPressedThisFrame) TryUse(smash);
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryUse(charge);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryUse(roll);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryUse(blink);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TryUse(conflagrate);
        if (Input.GetKeyDown(KeyCode.Alpha5)) TryUse(snipe);
        if (Input.GetKeyDown(KeyCode.Alpha6)) TryUse(smash);
#endif
    }

    void TryUse(SkillProfile s)
    {
        if (s == null) return;
        Debug.Log($"[Cast] {s.displayName} role={s.role} move={s.movement}");
        if (gate == null || gate.CanUse(s)) runner.RunSkill(s);
    }
}
