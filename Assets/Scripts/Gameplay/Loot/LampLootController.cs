using UnityEngine;
using Gameplay.Equipment;

namespace Gameplay.Loot
{
    public class LampLootController : MonoBehaviour
    {
        [SerializeField] private LampProgressionService lampService;
        [SerializeField] private EquipmentInventory inventory;
        [SerializeField] private EquipmentSlots equipmentSlots;
        [SerializeField] private GameLoopService gameLoop;

        private void Awake()
        {
            if (!lampService) lampService = FindObjectOfType<LampProgressionService>();
            if (!inventory) inventory = FindObjectOfType<EquipmentInventory>();
            if (!equipmentSlots) equipmentSlots = FindObjectOfType<EquipmentSlots>();
            if (!gameLoop) gameLoop = FindObjectOfType<GameLoopService>();
        }

        public bool RollAndStore()
        {
            if (lampService == null || inventory == null) return false;
            if (!lampService.TryConsumeCharge()) return false;
            int level = Mathf.Max(1, gameLoop?.Player?.Level ?? 1);
            var inst = lampService.RollOnce(level);
            if (inst == null) return false;
            inventory.Add(inst);
            AutoEquip(inst);
            return true;
        }

        private void AutoEquip(GearInstance inst)
        {
            if (equipmentSlots == null || inst?.item == null) return;
            var current = equipmentSlots.GetEquipped(inst.item.slot);
            if (current == null) equipmentSlots.Equip(inst);
        }
    }
}
