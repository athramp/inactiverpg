using System;
using UnityEngine;
using Gameplay.Equipment;

namespace Gameplay.Loot
{
    public class LampProgressionService : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private LampCatalog catalog;
        [SerializeField] private LootTable lootTable;
        [SerializeField] private SubstatCatalog substatCatalog;
        [SerializeField] private CurrencyService currency;
        public LootTable LootTableAsset => lootTable;

        [Header("Runtime")]
        [SerializeField] private int lampLevel = 1;
        [SerializeField] private float upgradeTimer;
        [SerializeField] private bool upgrading;

        public event Action OnLampChanged;
        public int LampLevel => lampLevel;
        public bool UpgradeInProgress => upgrading;
        public float UpgradeTimer => upgradeTimer;

        void Awake()
        {
            if (!currency) currency = FindObjectOfType<CurrencyService>();
        }

        void Update()
        {
            if (!upgrading) return;
            upgradeTimer -= Time.deltaTime;
            if (upgradeTimer <= 0f)
            {
                upgrading = false;
                lampLevel++;
                OnLampChanged?.Invoke();
            }
        }

        public LampLevelDef CurrentDef => catalog ? catalog.Get(lampLevel) : null;
        public LampLevelDef NextDef => catalog ? catalog.GetNext(lampLevel) : null;

        public bool TryStartUpgrade()
        {
            if (upgrading) return false;
            var next = NextDef;
            if (!next) return false;
            if (!currency || !currency.TrySpend(CurrencyType.Gold, next.upgradeGoldCost)) return false;

            upgrading = true;
            upgradeTimer = next.upgradeTimeSeconds;
            OnLampChanged?.Invoke();
            return true;
        }

        public bool TryConsumeCharge(int charges = 1)
        {
            return currency && currency.TrySpend(CurrencyType.LampCharge, charges);
        }

        public GearInstance RollOnce(int itemLevel)
        {
            if (!catalog || !lootTable) return null;
            var def = CurrentDef;
            if (def == null || def.rarityTable == null || def.rarityTable.Length == 0)
                return null;

            float total = 0f;
            foreach (var entry in def.rarityTable)
            {
                if (entry.requiresLevel > lampLevel) continue;
                total += Mathf.Max(0f, entry.weight);
            }
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, total);
            GearRarity rarity = GearRarity.Normal;
            foreach (var entry in def.rarityTable)
            {
                if (entry.requiresLevel > lampLevel) continue;
                float w = Mathf.Max(0f, entry.weight);
                if (roll <= w)
                {
                    rarity = entry.rarity;
                    break;
                }
                roll -= w;
            }

            var item = lootTable.GetRandom(rarity);
            if (!item) return null;
            return GearInstance.Create(item, rarity, itemLevel, substatCatalog);
        }
    }
}
