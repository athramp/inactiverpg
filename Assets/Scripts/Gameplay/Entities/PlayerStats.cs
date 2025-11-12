using System.Collections.Generic;
using UnityEngine;
using Gameplay.Equipment;

public class PlayerStats
{
    public string ClassId;
    public int Level = 1;
    public int CurrentXp;
    public int Hp;
    public int Atk;
    public int Def;
    [Header("Stat Growth (per level)")]
    [SerializeField] private int baseMaxHp = 100;
    [SerializeField] private int baseAtk   = 10;
    [SerializeField] private int baseDef   = 5;

    // flat increments per level (Lv1 uses base; LvN = base + (N-1)*perLevel)
    [SerializeField] private int hpPerLevel  = 10;
    [SerializeField] private int atkPerLevel = 2;
    [SerializeField] private int defPerLevel = 1;
    private ClassCatalog catalog;
    private XpTable xpTable;
    private EquipmentSlots equipmentSlots;
    private GearStatBlock equipmentBonus;
    public GearStatBlock EquipmentBonus => equipmentBonus;
    public event System.Action OnStatsChanged;
    private readonly Dictionary<GearSubstatType, float> substatTotals = new();
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

        if (!equipmentBonus.IsZero())
        {
            MaxHp += equipmentBonus.maxHp;
            Atk += equipmentBonus.attack;
            Def += equipmentBonus.defense;
        }
        OnStatsChanged?.Invoke();
    }
    public void ApplyLevelAndRecalculate(int newLevel, bool healToFull = true)
{
    Level = Mathf.Max(1, newLevel);

    // simple linear growth; replace with your curve if needed
    int levelsGained = Level - 1;
    int newMaxHp = baseMaxHp + levelsGained * hpPerLevel;
    int newAtk   = baseAtk   + levelsGained * atkPerLevel;
    int newDef   = baseDef   + levelsGained * defPerLevel;

    // assign through your existing setters if MaxHp/Atk/Def are protected
    MaxHp = newMaxHp;
    Atk   = newAtk;
    Def   = newDef;

    if (healToFull) Hp = MaxHp; else Hp = Mathf.Min(Hp, MaxHp);
}

    public void GainXp(int amount)
    {
        CurrentXp += amount;
        var xpToNext = xpTable.GetXpToNextLevel(Level);
        if (CurrentXp >= xpToNext)
        {
            Level++;
            CurrentXp -= xpToNext;
            RecalculateStats();
            Debug.Log($"Level Up! → {Level}");
        }
    }

    public void AttachEquipment(EquipmentSlots slots)
    {
        if (equipmentSlots == slots) return;
        if (equipmentSlots != null) equipmentSlots.OnEquipmentChanged -= HandleEquipmentChanged;
        equipmentSlots = slots;
        if (equipmentSlots != null) equipmentSlots.OnEquipmentChanged += HandleEquipmentChanged;
        RecalculateEquipmentBonus();
    }

    private void HandleEquipmentChanged() => RecalculateEquipmentBonus();

    private void RecalculateEquipmentBonus()
    {
        equipmentBonus = default;
        substatTotals.Clear();
        if (equipmentSlots != null)
        {
            foreach (var gear in equipmentSlots.AllEquipped())
            {
                if (gear == null) continue;
                equipmentBonus += gear.TotalStats;
                if (gear.Substats != null)
                {
                    foreach (var roll in gear.Substats)
                    {
                        if (substatTotals.ContainsKey(roll.type))
                            substatTotals[roll.type] += roll.value;
                        else
                            substatTotals[roll.type] = roll.value;
                    }
                }
            }
        }
        RecalculateStats();
        Hp = Mathf.Min(Hp, MaxHp);
    }

    public float GetSubstat(GearSubstatType type)
    {
        return substatTotals.TryGetValue(type, out var value) ? value : 0f;
    }
}
