using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Equipment
{
    [Serializable]
    public class GearInstance
    {
        public string instanceId;
        public GearItem item;
        public GearRarity rarity;
        public int level;

        [SerializeField] private GearStatBlock evaluatedStats;
        [SerializeField] private List<GearSubstatRoll> substats = new();

        public GearStatBlock TotalStats => evaluatedStats;
        public IReadOnlyList<GearSubstatRoll> Substats => substats;

        public static GearInstance Create(GearItem item, GearRarity rarity, int itemLevel, SubstatCatalog substatCatalog)
        {
            var inst = new GearInstance
            {
                instanceId = Guid.NewGuid().ToString("N"),
                item = item,
                rarity = rarity,
                level = Mathf.Max(1, itemLevel),
                evaluatedStats = item ? item.EvaluateStats(itemLevel, rarity) : default
            };
            inst.RollSubstats(substatCatalog);
            return inst;
        }

        public float GetSubstatValue(GearSubstatType type)
        {
            float total = 0f;
            foreach (var roll in substats)
            {
                if (roll.type == type)
                    total += roll.value;
            }
            return total;
        }

        private void RollSubstats(SubstatCatalog catalog)
        {
            substats.Clear();
            if (catalog == null || item == null) return;

            int desired = catalog.GetSubstatCount(rarity);
            if (desired <= 0) return;

            var pool = catalog.GetEligibleDefinitions(rarity, new List<SubstatDefinition>());
            if (pool.Count == 0) return;

            for (int i = 0; i < desired && pool.Count > 0; i++)
            {
                var def = catalog.PickRandomDefinition(pool, rarity);
                if (def == null) break;

                if (!def.TryGetRange(rarity, out var range, out _))
                    break;

                float val = UnityEngine.Random.Range(range.x, range.y);
                substats.Add(new GearSubstatRoll { type = def.type, value = val });

                if (!def.allowDuplicates)
                    pool.RemoveAll(p => p.type == def.type);
            }
        }
    }
}
