using UnityEngine;

namespace Battle.Core
{
    [System.Serializable]
    public struct StatBlock
    {
        public int Hp;
        public int Atk;
        public int Def;
        public float CritChance;
        public float CritMult;

        public static StatBlock From(int hp, int atk, int def, float critChance = 0f, float critMult = 1f)
            => new StatBlock { Hp = hp, Atk = atk, Def = def, CritChance = critChance, CritMult = critMult };
    }
}
