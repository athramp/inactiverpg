using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Equipment
{
    public class EquipmentSlots : MonoBehaviour
    {
        [Serializable]
        public class SlotEntry
        {
            public GearSlot slot;
            public GearInstance equipped;
        }

        [SerializeField] private List<SlotEntry> slots = new()
        {
            new SlotEntry{ slot = GearSlot.Weapon },
            new SlotEntry{ slot = GearSlot.Helmet },
            new SlotEntry{ slot = GearSlot.Chest },
            new SlotEntry{ slot = GearSlot.Gloves },
            new SlotEntry{ slot = GearSlot.Boots },
            new SlotEntry{ slot = GearSlot.Accessory },
        };

        public event Action OnEquipmentChanged;

        public IEnumerable<GearInstance> AllEquipped()
        {
            foreach (var entry in slots)
                if (entry.equipped != null)
                    yield return entry.equipped;
        }

        public IEnumerable<(GearSlot slot, GearInstance gear)> EnumerateSlots()
        {
            foreach (var entry in slots)
                yield return (entry.slot, entry.equipped);
        }

        public bool Equip(GearInstance inst)
        {
            if (inst?.item == null) return false;
            var slot = slots.Find(s => s.slot == inst.item.slot);
            if (slot == null) return false;
            slot.equipped = inst;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public bool Unequip(GearSlot slotType)
        {
            var slot = slots.Find(s => s.slot == slotType);
            if (slot == null || slot.equipped == null) return false;
            slot.equipped = null;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public GearInstance GetEquipped(GearSlot slotType)
            => slots.Find(s => s.slot == slotType)?.equipped;

        public float GetTotalSubstat(GearSubstatType type)
        {
            float total = 0f;
            foreach (var gear in AllEquipped())
            {
                if (gear == null) continue;
                total += gear.GetSubstatValue(type);
            }
            return total;
        }

        public void ApplyLoadout(Dictionary<GearSlot, GearInstance> mapping)
        {
            bool changed = false;
            foreach (var entry in slots)
            {
                if (mapping != null && mapping.TryGetValue(entry.slot, out var inst))
                {
                    entry.equipped = inst;
                    changed = true;
                }
                else if (entry.equipped != null)
                {
                    entry.equipped = null;
                    changed = true;
                }
            }
            if (changed) OnEquipmentChanged?.Invoke();
        }
    }
}
