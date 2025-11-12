using UnityEngine;

namespace Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "InactiveRPG/Gear Item")]
    public class GearItem : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "New Gear";
        public GearSlot slot = GearSlot.Weapon;
        public Sprite icon;

        [Header("Base Stats (rarity agnostic)")]
        public GearStatBlock baseStats;

        [Header("Level Scaling (added on top of base stats)")]
        public AnimationCurve attackByLevel = AnimationCurve.Linear(1f, 0f, 100f, 0f);
        public AnimationCurve defenseByLevel = AnimationCurve.Linear(1f, 0f, 100f, 0f);
        public AnimationCurve hpByLevel = AnimationCurve.Linear(1f, 0f, 100f, 0f);

        [Header("Rarity Scaling (flat bonus per rarity tier)")]
        public GearRarityScaling[] rarityScaling;

        public GearStatBlock EvaluateStats(int level, GearRarity rarity)
        {
            level = Mathf.Max(1, level);
            var stats = baseStats;
            if (attackByLevel != null) stats.attack += Mathf.RoundToInt(attackByLevel.Evaluate(level));
            if (defenseByLevel != null) stats.defense += Mathf.RoundToInt(defenseByLevel.Evaluate(level));
            if (hpByLevel != null) stats.maxHp += Mathf.RoundToInt(hpByLevel.Evaluate(level));

            if (rarityScaling != null)
            {
                foreach (var scaling in rarityScaling)
                {
                    if (scaling.rarity == rarity)
                    {
                        stats += scaling.additiveBonus;
                        break;
                    }
                }
            }
            return stats;
        }
    }

    [System.Serializable]
    public struct GearRarityScaling
    {
        public GearRarity rarity;
        public GearStatBlock additiveBonus;
    }
}
