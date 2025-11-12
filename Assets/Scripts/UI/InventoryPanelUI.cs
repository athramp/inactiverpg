using System.Collections.Generic;
using UnityEngine;
using Gameplay.Equipment;
public class InventoryPanelUI : MonoBehaviour
{
    [SerializeField] EquipmentInventory inventory;
    [SerializeField] EquipmentSlots equipmentSlots;
    [SerializeField] Transform contentRoot;
    [SerializeField] InventoryItemView itemPrefab;

    List<InventoryItemView> views = new();

    void Awake()
    {
        if (!inventory) inventory = FindObjectOfType<EquipmentInventory>();
        if (!equipmentSlots) equipmentSlots = FindObjectOfType<EquipmentSlots>();
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
            view.Bind(item, equipmentSlots.GetEquipped(item.item.slot) == item, OnEquipPressed);
            views.Add(view);
        }
    }

    void OnEquipPressed(GearInstance inst)
    {
        equipmentSlots.Equip(inst);
    }
}
