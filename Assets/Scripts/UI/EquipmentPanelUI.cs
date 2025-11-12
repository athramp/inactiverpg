using UnityEngine;
using Gameplay.Equipment;

public class EquipmentPanelUI : MonoBehaviour
{
    [SerializeField] EquipmentSlots equipmentSlots;
    [SerializeField] EquipmentSlotView[] slotViews;

    void Awake()
    {
        if (!equipmentSlots) equipmentSlots = FindObjectOfType<EquipmentSlots>();
    }
    void OnEnable()
    {
        Refresh();
        equipmentSlots.OnEquipmentChanged += Refresh;
    }
    void OnDisable()
    {
        equipmentSlots.OnEquipmentChanged -= Refresh;
    }

    void Refresh() {
    Debug.Log($"EquipmentPanelUI Refresh, slots={slotViews.Length}");
    foreach (var view in slotViews) {
        var inst = equipmentSlots.GetEquipped(view.slotType);
        Debug.Log($" - {view.slotType} = {inst?.item?.displayName ?? "null"}");
        view.Bind(inst);
    }
}

}
