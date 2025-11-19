using System.Collections.Generic;
using UnityEngine;
using Gameplay.Equipment;
public class InventoryPanelUI : MonoBehaviour
{
    [SerializeField] EquipmentInventory inventory;
    [SerializeField] EquipmentSlots equipmentSlots;
    [SerializeField] CurrencyService currencyService;
    [SerializeField] Transform contentRoot;
    [SerializeField] InventoryItemView itemPrefab;

    List<InventoryItemView> views = new();

    void Awake()
    {
        if (!inventory) inventory = FindObjectOfType<EquipmentInventory>();
        if (!equipmentSlots) equipmentSlots = FindObjectOfType<EquipmentSlots>();
        if (!currencyService) currencyService = FindObjectOfType<CurrencyService>();
    }

    void OnEnable()
    {
        Rebuild();
        inventory.OnItemAdded += _ => Rebuild();
        inventory.OnItemRemoved += _ => Rebuild();
        equipmentSlots.OnEquipmentChanged += Rebuild;
    }
    void OnDisable()
    {
        inventory.OnItemAdded -= _ => Rebuild(); // cache delegates if you prefer
        inventory.OnItemRemoved -= _ => Rebuild();
        equipmentSlots.OnEquipmentChanged -= Rebuild;
    }

    void Rebuild()
    {
        foreach (var view in views) Destroy(view.gameObject);
        views.Clear();
        foreach (var item in inventory.Items)
        {
            var view = Instantiate(itemPrefab, contentRoot);
            bool equipped = equipmentSlots.GetEquipped(item.item.slot) == item;
            long value = CalculateSellValue(item);
            view.Bind(item, equipped, OnEquipPressed, OnSellPressed, value);
            views.Add(view);
        }
    }

    void OnEquipPressed(GearInstance inst)
    {
        equipmentSlots.Equip(inst);
    }

    void OnSellPressed(GearInstance inst)
    {
        if (inst == null || inventory == null || currencyService == null) return;
        bool removed = inventory.Remove(inst.instanceId);
        if (removed)
        {
            long value = CalculateSellValue(inst);
            currencyService.Add(CurrencyType.Gold, value);
            Rebuild();
        }
    }

    long CalculateSellValue(GearInstance inst)
    {
        if (inst == null) return 0;
        float rarityMult = inst.rarity switch
        {
            GearRarity.Common    => 1.0f,
            GearRarity.Uncommon  => 1.3f,
            GearRarity.Rare      => 1.69f,
            GearRarity.Epic      => 2.2f,
            GearRarity.Legendary => 2.86f,
            GearRarity.Mythic    => 3.71f,
            GearRarity.Ancient   => 4.82f,
            GearRarity.Relic     => 6.27f,
            GearRarity.Exalted   => 8.15f,
            GearRarity.Celestial => 10.0f,
            _ => 1f
        };
        var stats = inst.TotalStats;
        float statBonus = stats.attack * 4f + stats.defense * 3f + stats.maxHp * 0.5f;
        float baseValue = 25f + inst.level * 6f;
        long value = Mathf.RoundToInt((baseValue + statBonus) * rarityMult);
        return value < 5 ? 5 : value;
    }
}
