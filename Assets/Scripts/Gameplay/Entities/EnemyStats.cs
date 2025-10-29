// EnemyStats.cs
public class EnemyStats
{
    public string MonsterId;
    public int Level;

    public int Hp;       // current
    public int HpMax;    // from def (post-scaling)
    public int Atk;      // from def (post-scaling)
    public int Def;      // from def (post-scaling)
    public float CritChance;
    public float CritMult;

    public int XpReward;

    public EnemyStats(int level)
    {
        Level = level;
        // Defaults only; will be overwritten by MonsterDef in SpawnEnemy()
        HpMax = 50 + level * 10;
        Hp    = HpMax;
        Atk   = 10 + level * 2;
        Def   = 5 + level;
        CritChance = 0f;
        CritMult   = 1.5f;
        XpReward   = 25 + level * 5;
        MonsterId  = "Unknown";
    }
}
