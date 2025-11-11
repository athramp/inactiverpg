// Assets/Scripts/Gameplay/UI/SkillQuickCast.cs
using UnityEngine;

public class SkillQuickCast : MonoBehaviour
{
    [Header("References")]
    public SkillLoadout loadout;
    public SkillRunner runner;

    /// <summary>
    /// Casts the skill assigned to the given slot index (0-based).
    /// Hook your HUD buttons to this.
    /// </summary>
    public void CastSlot(int index)
    {
        if (!loadout || !runner) return;
        var skill = loadout.GetSlot(index);
        if (skill)
        {
            runner.RunSkill(skill);
        }
        else
        {
            Debug.Log($"[SkillQuickCast] No skill assigned to slot {index}");
        }
    }
}
