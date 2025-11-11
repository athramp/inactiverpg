using UnityEngine;

public class SkillGate : MonoBehaviour
{
    public PlayerSpaceCoordinator coordinator;

    float SelfX  => coordinator.LogicalX; // keep as-is for your PSC
    float EnemyX => CombatOrchestrator.Instance
        ? CombatOrchestrator.Instance.EnemyLogicalX()   // <-- invoke the shim method
        : (SelfX + 5f);

    public bool CanUse(SkillProfile s)
    {
        float dist = Mathf.Abs(EnemyX - SelfX);
        if (dist < s.minRangeToUse) return false;
        if (dist > s.maxRangeToUse) return false;
        return true;
    }
}
