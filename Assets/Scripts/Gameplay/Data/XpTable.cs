using UnityEngine;

[CreateAssetMenu(fileName = "XpTable", menuName = "Game/Data/Xp Table")]
public class XpTable : ScriptableObject
{
    [Tooltip("XP required to reach level (index). Entry 0 is unused; Entry 1 is XP to reach lvl 1 (usually 0).")]
    public int[] xpToReachLevel = new int[] { 0, 0, 50, 150, 300, 500, 750, 1050 };

    public int MaxLevel => (xpToReachLevel != null && xpToReachLevel.Length > 0) ? xpToReachLevel.Length - 1 : 1;

    public int GetXpToReachLevel(int level)
    {
        if (xpToReachLevel == null || xpToReachLevel.Length == 0) return 0;
        level = Mathf.Clamp(level, 0, MaxLevel);
        return xpToReachLevel[level];
    }

    public int GetXpToNextLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel) return int.MaxValue; // cap
        return GetXpToReachLevel(currentLevel + 1) - GetXpToReachLevel(currentLevel);
    }
}
