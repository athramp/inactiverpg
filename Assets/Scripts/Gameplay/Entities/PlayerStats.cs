using UnityEngine;

public class PlayerStats
{
    public string ClassId;
    public int Level = 1;
    public int CurrentXp;
    public int Hp;
    public int Atk;
    public int Def;

    private ClassCatalog catalog;
    private XpTable xpTable;
    public void ApplyProgress(string classId, int level, int xp, int hp)
{
    if (!string.IsNullOrEmpty(classId)) ClassId = classId;
    Level = Mathf.Max(1, level);
    CurrentXp = Mathf.Max(0, xp);
    RecalculateStats();
    Hp = Mathf.Clamp(hp <= 0 ? MaxHp : hp, 1, MaxHp);
}

public System.Collections.Generic.Dictionary<string, object> ToFirestoreProgress()
{
    return new System.Collections.Generic.Dictionary<string, object> {
        { "class", ClassId },
        { "level", Level },
        { "xp", CurrentXp },
        { "hp", Mathf.Clamp(Hp, 0, MaxHp) },
        { "updatedAt", Firebase.Firestore.FieldValue.ServerTimestamp }
    };
}

    public PlayerStats(ClassCatalog catalog, XpTable xpTable, string classId)
    {
        this.catalog = catalog;
        this.xpTable = xpTable;
        this.ClassId = classId;
        RecalculateStats();
        Hp = MaxHp; // start full
    }

    public int MaxHp { get; private set; }

    public void RecalculateStats()
    {
        var c = catalog.Get(ClassId);
        MaxHp = Mathf.RoundToInt(c.BaseHP + c.HpGrowth * (Level - 1));
        Atk = Mathf.RoundToInt(c.BaseATK + c.AtkGrowth * (Level - 1));
        Def = Mathf.RoundToInt(c.BaseDEF + c.DefGrowth * (Level - 1));
    }

    public void GainXp(int amount)
    {
        CurrentXp += amount;
        var xpToNext = xpTable.GetXpToNext(Level);
        if (CurrentXp >= xpToNext)
        {
            Level++;
            CurrentXp -= xpToNext;
            RecalculateStats();
            Debug.Log($"Level Up! → {Level}");
        }
    }
}
