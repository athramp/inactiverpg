using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatMap : Dictionary<string, float> { }

public static class StatKeys {
    public const string HP = "hp";
    public const string ATK = "atk";
    public const string DEF = "def";
    public const string CRIT_CHANCE = "crit_chance";
    public const string CRIT_MULT = "crit_mult";
    public const string SPD = "spd";
}

public class StatsView
{
    private readonly StatMap s;
    public StatsView(StatMap map) { s = map ?? new StatMap(); }
    float Get(string k, float d) => s != null && s.TryGetValue(k, out var v) ? v : d;

    public int HP         => Mathf.FloorToInt(Get(StatKeys.HP, 100));
    public int ATK        => Mathf.FloorToInt(Get(StatKeys.ATK, 5));
    public int DEF        => Mathf.FloorToInt(Get(StatKeys.DEF, 0));
    public float Crit     => Get(StatKeys.CRIT_CHANCE, 0.05f);
    public float CritMult => Get(StatKeys.CRIT_MULT, 1.5f);
    public int SPD        => Mathf.FloorToInt(Get(StatKeys.SPD, 10));
}
