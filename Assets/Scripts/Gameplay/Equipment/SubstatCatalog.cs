using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "InactiveRPG/Substat Catalog")]
    public class SubstatCatalog : ScriptableObject
    {
        public RaritySubstatBudget[] rarityBudgets;
        public SubstatDefinition[] substats;

        public int GetSubstatCount(GearRarity rarity)
        {
            if (rarityBudgets == null) return 0;
            foreach (var entry in rarityBudgets)
            {
                if (entry.rarity == rarity) return Mathf.Max(0, entry.substatCount);
            }
            return 0;
        }

        public List<SubstatDefinition> GetEligibleDefinitions(GearRarity rarity, List<SubstatDefinition> buffer)
        {
            buffer ??= new List<SubstatDefinition>();
            buffer.Clear();
            if (substats == null) return buffer;
            foreach (var def in substats)
            {
                if (def != null && def.HasRange(rarity))
                    buffer.Add(def);
            }
            return buffer;
        }

        public SubstatDefinition PickRandomDefinition(List<SubstatDefinition> pool, GearRarity rarity)
        {
            if (pool == null || pool.Count == 0) return null;
            float total = 0f;
            foreach (var def in pool)
            {
                total += Mathf.Max(0.0001f, def.GetWeight(rarity));
            }
            float roll = UnityEngine.Random.Range(0f, total);
            foreach (var def in pool)
            {
                float w = Mathf.Max(0.0001f, def.GetWeight(rarity));
                if (roll <= w) return def;
                roll -= w;
            }
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }
    }

    [Serializable]
    public struct RaritySubstatBudget
    {
        public GearRarity rarity;
        [Tooltip("How many substats items of this rarity roll.")]
        public int substatCount;
    }

    [Serializable]
    public class SubstatDefinition
    {
        public GearSubstatType type;
        public bool allowDuplicates;
        public RaritySubstatRange[] rarityRanges;

        public bool HasRange(GearRarity rarity)
        {
            return TryGetRange(rarity, out _, out _);
        }

        public bool TryGetRange(GearRarity rarity, out Vector2 range, out float weight)
        {
            if (rarityRanges != null)
            {
                foreach (var rr in rarityRanges)
                {
                    if (rr.rarity == rarity)
                    {
                        range = rr.valuePercentRange;
                        weight = rr.weight <= 0f ? 1f : rr.weight;
                        return true;
                    }
                }
            }
            range = Vector2.zero;
            weight = 0f;
            return false;
        }

        public float GetWeight(GearRarity rarity)
        {
            return TryGetRange(rarity, out _, out var weight) ? weight : 0f;
        }
    }

    [Serializable]
    public struct RaritySubstatRange
    {
        public GearRarity rarity;
        [Tooltip("Value stored as percent (e.g., 2 means +2%).")]
        public Vector2 valuePercentRange;
        public float weight;
    }
}
