using UnityEngine;

[System.Serializable]
public class PlayerRuntime
{
    public int Level = 1;
    public float XP = 0;

    public StatMap Final = new StatMap {
        { StatKeys.HP, 100 }, { StatKeys.ATK, 5 }, { StatKeys.DEF, 0 },
        { StatKeys.CRIT_CHANCE, 0.05f }, { StatKeys.CRIT_MULT, 1.5f }, { StatKeys.SPD, 10 }
    };

    public int CurrentHP;
    public StatsView View => new StatsView(Final);

    public void InitHP() => CurrentHP = View.HP;

    public float XpNeeded(int lvl) => 100f * lvl * 1.5f; // simple curve
    public bool GainXP(int amount)
    {
        XP += amount;
        bool leveled = false;
        while (XP >= XpNeeded(Level))
        {
            XP -= XpNeeded(Level);
            Level++;
            // simple growth; can tune later
            Final[StatKeys.HP] = View.HP + 20;
            Final[StatKeys.ATK] = View.ATK + 2;
            Final[StatKeys.DEF] = View.DEF + 1;
            InitHP(); // heal on level up (optional)
            leveled = true;
        }
        return leveled;
    }
}

[System.Serializable]
public class EnemyRuntime
{
    public string MonsterId;
    public int XpReward = 10;
    public StatMap Final = new StatMap {
        { StatKeys.HP, 80 }, { StatKeys.ATK, 6 }, { StatKeys.DEF, 1 },
        { StatKeys.CRIT_CHANCE, 0.02f }, { StatKeys.CRIT_MULT, 1.5f }, { StatKeys.SPD, 8 }
    };
    public int CurrentHP;
    public StatsView View => new StatsView(Final);

    public void InitHP() => CurrentHP = View.HP;
}
