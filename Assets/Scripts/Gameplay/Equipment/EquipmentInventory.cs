using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Equipment
{
    public class EquipmentInventory : MonoBehaviour
    {
        [SerializeField] private List<GearInstance> items = new();
        public IReadOnlyList<GearInstance> Items => items;

        public event Action<GearInstance> OnItemAdded;
        public event Action<GearInstance> OnItemRemoved;

        public void Add(GearInstance instance)
        {
            if (instance == null) return;
            items.Add(instance);
            OnItemAdded?.Invoke(instance);
        }

        public bool Remove(string instanceId)
        {
            int idx = items.FindIndex(i => i.instanceId == instanceId);
            if (idx >= 0)
            {
                var inst = items[idx];
                items.RemoveAt(idx);
                OnItemRemoved?.Invoke(inst);
                return true;
            }
            return false;
        }

        public GearInstance Find(string instanceId) => items.Find(i => i.instanceId == instanceId);
    }
}
