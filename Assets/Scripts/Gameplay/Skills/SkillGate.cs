using UnityEngine;
public class SkillGate : MonoBehaviour
{
    public PlayerSpaceCoordinator coordinator;
    public Transform enemyLogicalAnchor; // or reference to enemy coordinator if you add one
    public float enemyLogicalX;          // supply this from your enemy’s 1D logic

    float SelfX => coordinator.logicalX;

    public bool CanUse(SkillProfile s)
    {
        float dist = Mathf.Abs(enemyLogicalX - SelfX); // 1D logical distance
        if (dist < s.minRangeToUse) return false;
        if (dist > s.maxRangeToUse) return false;
        return true;
    }
}
