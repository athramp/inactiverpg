using System;
using UnityEngine;
using Gameplay.Equipment;

namespace Gameplay.Loot
{
    [CreateAssetMenu(menuName = "InactiveRPG/Lamp Level Definition")]
    public class LampLevelDef : ScriptableObject
    {
        public int level = 1;
        public int upgradeGoldCost = 100;
        public float upgradeTimeSeconds = 30f;
        [Tooltip("Lamp charges generated per hour at this level/stage baseline.")]
        public float chargesPerHour = 10f;

        public RarityChance[] rarityTable;
    }

    [Serializable]
    public struct RarityChance
    {
        public GearRarity rarity;
        [Range(0f, 1f)] public float weight;
        public int requiresLevel;
    }
}
