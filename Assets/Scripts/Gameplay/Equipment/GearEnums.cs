using System;
using UnityEngine;

namespace Gameplay.Equipment
{
    public enum GearSlot
    {
        Weapon,
        Helmet,
        Chest,
        Gloves,
        Boots,
        Accessory
    }

    public enum GearRarity
    {
        Normal,
        Unique,
        Well,
        Rare,
        Mythic,
        Epic,
        Legendary,
        Immortal,
        Supreme,
        Aurous,
        Eternal
    }

    public enum GearSubstatType
    {
        CritRate,
        ReflectDamage,
        Evasion,
        StunChance,
        SkillCritRate
    }

    [Serializable]
    public struct GearStatBlock
    {
        public int attack;
        public int defense;
        public int maxHp;

        public static GearStatBlock operator +(GearStatBlock a, GearStatBlock b)
        {
            return new GearStatBlock
            {
                attack = a.attack + b.attack,
                defense = a.defense + b.defense,
                maxHp = a.maxHp + b.maxHp
            };
        }

        public bool IsZero() => attack == 0 && defense == 0 && maxHp == 0;
    }

    [Serializable]
    public struct GearSubstatRoll
    {
        public GearSubstatType type;
        public float value;
    }
}
