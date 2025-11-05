// Assets/Scripts/Gameplay/Skills/SkillInputTest.cs
using UnityEngine;

public class SkillInputTest : MonoBehaviour
{
    [Header("Refs")]
    public SkillRunner runner;

    [Header("Bind some skills here for quick testing")]
    public SkillProfile warriorCharge;
    public SkillProfile archerRoll;
    public SkillProfile mageBlink;

    // Optional damage skills you make later:
    public SkillProfile mageConflagrate;
    public SkillProfile archerSnipe;
    public SkillProfile warriorSmash;

    void Update()
    {
        if (!runner) return;

        // Movement / displacement
        if (Input.GetKeyDown(KeyCode.Alpha1) && warriorCharge)  runner.RunSkill(warriorCharge);
        if (Input.GetKeyDown(KeyCode.Alpha2) && archerRoll)     runner.RunSkill(archerRoll);
        if (Input.GetKeyDown(KeyCode.Alpha3) && mageBlink)      runner.RunSkill(mageBlink);

        // Pure damage examples
        if (Input.GetKeyDown(KeyCode.Alpha7) && mageConflagrate) runner.RunSkill(mageConflagrate);
        if (Input.GetKeyDown(KeyCode.Alpha8) && archerSnipe)     runner.RunSkill(archerSnipe);
        if (Input.GetKeyDown(KeyCode.Alpha9) && warriorSmash)    runner.RunSkill(warriorSmash);
    }
}
