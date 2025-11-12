using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay.Equipment;

namespace Gameplay.Loot
{
    [CreateAssetMenu(menuName = "InactiveRPG/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [SerializeField] private List<RarityPool> pools = new();

        public GearItem GetRandom(GearRarity rarity)
        {
            var pool = pools.Find(p => p.rarity == rarity);
            if (pool == null || pool.items == null || pool.items.Count == 0)
                return null;
            int idx = Random.Range(0, pool.items.Count);
            return pool.items[idx];
        }

        public GearItem FindItem(string name)
        {
            if (string.IsNullOrEmpty(name) || pools == null) return null;
            foreach (var pool in pools)
            {
                if (pool?.items == null) continue;
                foreach (var item in pool.items)
                    if (item && item.name == name)
                        return item;
            }
            return null;
        }

        [System.Serializable]
        private class RarityPool
        {
            public GearRarity rarity;
            public List<GearItem> items = new();
        }
    }
}
