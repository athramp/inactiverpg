using UnityEngine;
using System;

[Serializable]
public struct Growth // per-level additive growth
{
    public int hpPerLevel;
    public int atkPerLevel;
    public int defPerLevel;
}

public class PlayerProgression : MonoBehaviour
{
    [Header("Data")]
    public XpTable xpTable;

    [Header("Growth per Level")]
    public Growth growth = new Growth { hpPerLevel = 5, atkPerLevel = 1, defPerLevel = 1 };

    // Backing state. Source of truth for progression.
    [SerializeField] private int level = 1;
    [SerializeField] private int xpTotal = 0;

    public int Level => level;
    public int XpTotal => xpTotal;
    public int XpIntoLevel => xpTotal - xpTable.GetXpToReachLevel(level);
    public int XpToNextLevel => xpTable.GetXpToNextLevel(level);

    public event Action<int> OnLevelUp;        // (newLevel)
    public event Action<int,int> OnXpChanged;  // (newXpTotal, delta)

    public void InitializeFromSave(int savedLevel, int savedXpTotal)
    {
        level = Mathf.Max(1, savedLevel);
        xpTotal = Mathf.Max(0, savedXpTotal);
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        xpTotal += amount;
        OnXpChanged?.Invoke(xpTotal, amount);

        while (level < xpTable.MaxLevel && xpTotal >= xpTable.GetXpToReachLevel(level + 1))
        {
            level++;
            OnLevelUp?.Invoke(level);
        }
    }

    // Apply level-based growth to base stats and return final stats.
    public void ApplyLevelGrowth(ref int maxHp, ref int atk, ref int def, int baseLevel = 1)
    {
        int levelsGained = Mathf.Max(0, level - baseLevel);
        maxHp += growth.hpPerLevel * levelsGained;
        atk   += growth.atkPerLevel * levelsGained;
        def   += growth.defPerLevel * levelsGained;
    }
}
