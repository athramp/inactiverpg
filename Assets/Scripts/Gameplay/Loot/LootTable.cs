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

        [System.Serializable]
        private class RarityPool
        {
            public GearRarity rarity;
            public List<GearItem> items = new();
        }
    }
}
