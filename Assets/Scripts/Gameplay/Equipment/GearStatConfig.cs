using UnityEngine;

namespace Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "InactiveRPG/Gear Stat Config")]
    public class GearStatConfig : ScriptableObject
    {
        [Header("Base Values (Level 1 Common)")]
        public float hpBase = 80f;
        public float atkBase = 6f;
        public float defBase = 2f;

        [Header("Per Level Growth (exponential)")]
        public float hpGrowth = 1.04479f;
        public float atkGrowth = 1.03981f;
        public float defGrowth = 1.03981f;

        [Header("Rarity Multipliers")]
        public RarityMultiplier[] rarityMultipliers = new[]
        {
            new RarityMultiplier{ rarity = GearRarity.Common,    multiplier = 1.00f },
            new RarityMultiplier{ rarity = GearRarity.Uncommon,  multiplier = 1.30f },
            new RarityMultiplier{ rarity = GearRarity.Rare,      multiplier = 1.69f },
            new RarityMultiplier{ rarity = GearRarity.Epic,      multiplier = 2.20f },
            new RarityMultiplier{ rarity = GearRarity.Legendary, multiplier = 2.86f },
            new RarityMultiplier{ rarity = GearRarity.Mythic,    multiplier = 3.71f },
            new RarityMultiplier{ rarity = GearRarity.Ancient,   multiplier = 4.82f },
            new RarityMultiplier{ rarity = GearRarity.Relic,     multiplier = 6.27f },
            new RarityMultiplier{ rarity = GearRarity.Exalted,   multiplier = 8.15f },
            new RarityMultiplier{ rarity = GearRarity.Celestial, multiplier = 8.15f },
        };

        [Header("Variance (per item)")]
        [Range(0.5f, 1f)] public float varianceMin = 0.95f;
        [Range(1f, 1.5f)] public float varianceMax = 1.05f;

        [Header("Special high-end bonus (Celestial)")]
        public GearRarity specialRarity = GearRarity.Celestial;
        public float specialHpBonus = 4.05f;
        public float specialAtkBonus = 1.38f;
        public float specialDefBonus = 1.55f;

        public bool TryGetMultiplier(GearRarity rarity, out float multiplier)
        {
            foreach (var entry in rarityMultipliers)
            {
                if (entry.rarity == rarity)
                {
                    multiplier = Mathf.Max(0.01f, entry.multiplier);
                    return true;
                }
            }
            multiplier = 1f;
            return false;
        }

        [System.Serializable]
        public struct RarityMultiplier
        {
            public GearRarity rarity;
            public float multiplier;
        }
    }
}
