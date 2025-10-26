using UnityEngine;

public class EnemyStats
{
    public int Level;
    public int Hp;
    public int Atk;
    public int Def;
    public int HpMax => 50 + Level * 10;


    public EnemyStats(int level)
    {
        Level = level;
        Hp = 50 + level * 10;
        Atk = 10 + level * 2;
        Def = 5 + level;
    }
}
