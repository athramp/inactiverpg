using UnityEngine;

namespace Gameplay.Equipment
{
    public static class GearStatCalculator
    {
        private const string ConfigResourcePath = "GearStatConfig";
        private static GearStatConfig _config;

        private static GearStatConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = Resources.Load<GearStatConfig>(ConfigResourcePath);
                    if (_config == null)
                        Debug.LogError($"[GearStatCalculator] Missing GearStatConfig at Resources/{ConfigResourcePath}.asset");
                }
                return _config;
            }
        }

        public static GearStatBlock RollStats(GearItem item, GearRarity rarity, int level)
        {
            var cfg = Config;
            if (cfg == null) return default;

            level = Mathf.Max(1, level);

            if (!cfg.TryGetMultiplier(rarity, out var rarityMult))
                rarityMult = 1f;

            bool isSpecial = rarity == cfg.specialRarity;

            float hp = cfg.hpBase * Mathf.Pow(cfg.hpGrowth, level - 1) * rarityMult;
            float atk = cfg.atkBase * Mathf.Pow(cfg.atkGrowth, level - 1) * rarityMult;
            float def = cfg.defBase * Mathf.Pow(cfg.defGrowth, level - 1) * rarityMult;

            float variance = Random.Range(cfg.varianceMin, cfg.varianceMax);
            hp *= variance;
            atk *= variance;
            def *= variance;

            if (isSpecial)
            {
                hp *= cfg.specialHpBonus;
                atk *= cfg.specialAtkBonus;
                def *= cfg.specialDefBonus;
            }

            return new GearStatBlock
            {
                maxHp = Mathf.Max(1, Mathf.RoundToInt(hp)),
                attack = Mathf.Max(1, Mathf.RoundToInt(atk)),
                defense = Mathf.Max(1, Mathf.RoundToInt(def))
            };
        }
    }
}
