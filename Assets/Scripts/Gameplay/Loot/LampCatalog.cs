using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Loot
{
    [CreateAssetMenu(menuName = "InactiveRPG/Lamp Catalog")]
    public class LampCatalog : ScriptableObject
    {
        public List<LampLevelDef> levels;

        public LampLevelDef Get(int level)
        {
            if (levels == null || levels.Count == 0) return null;
            return levels.Find(l => l && l.level == level) ?? levels[Mathf.Clamp(level - 1, 0, levels.Count - 1)];
        }

        public LampLevelDef GetNext(int level) => Get(level + 1);
    }
}
