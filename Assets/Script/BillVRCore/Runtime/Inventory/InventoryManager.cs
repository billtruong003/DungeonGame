using System;
using System.Collections.Generic;
using UnityEngine;
using BillVRCore.Interaction;

namespace BillVRCore.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [SerializeField] private List<BodySlot> slots = new();

        public IReadOnlyList<BodySlot> AllSlots => slots;
        public int SlotCount => slots.Count;
        public Action<BodySlot, Grabbable> OnItemStored;
        public Action<BodySlot, Grabbable> OnItemRemoved;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            if (slots.Count == 0)
                GetComponentsInChildren(false, slots);

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].PlacePoint.OnPlaceEvent += HandleItemPlaced;
                slots[i].PlacePoint.OnRemoveEvent += HandleItemRemoved;
            }
        }

        public BodySlot FindSlotForItem(InventoryItem item)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].HasItem && slots[i].CanAcceptItem(item))
                    return slots[i];
            }
            return null;
        }

        public BodySlot FindSlotByType(SlotType type)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Type == type) return slots[i];
            }
            return null;
        }

        public BodySlot FindClosestAccessibleSlot(Vector3 handPosition)
        {
            BodySlot closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsAccessible(handPosition)) continue;

                float dist = Vector3.Distance(handPosition, slots[i].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = slots[i];
                }
            }
            return closest;
        }

        public int GetFilledSlotCount()
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasItem) count++;
            }
            return count;
        }

        public int GetEmptySlotCount()
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].HasItem) count++;
            }
            return count;
        }

        public void GetFilledSlots(List<BodySlot> results)
        {
            results.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].HasItem) results.Add(slots[i]);
            }
        }

        public void GetEmptySlots(List<BodySlot> results)
        {
            results.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].HasItem) results.Add(slots[i]);
            }
        }

        public bool StoreItem(InventoryItem item)
        {
            var slot = FindSlotForItem(item);
            if (slot == null) return false;
            slot.PlacePoint.Place(item.Grabbable);
            return true;
        }

        public Grabbable RetrieveFromSlot(SlotType type)
        {
            var slot = FindSlotByType(type);
            if (slot == null || !slot.HasItem) return null;
            var item = slot.StoredItem;
            slot.PlacePoint.Remove();
            return item;
        }

        public bool HasItemOfType(SlotType slotType)
        {
            var slot = FindSlotByType(slotType);
            return slot != null && slot.HasItem;
        }

        public void RegisterSlot(BodySlot slot)
        {
            if (slots.Contains(slot)) return;
            slots.Add(slot);
            slot.PlacePoint.OnPlaceEvent += HandleItemPlaced;
            slot.PlacePoint.OnRemoveEvent += HandleItemRemoved;
        }

        public void UnregisterSlot(BodySlot slot)
        {
            if (!slots.Remove(slot)) return;
            slot.PlacePoint.OnPlaceEvent -= HandleItemPlaced;
            slot.PlacePoint.OnRemoveEvent -= HandleItemRemoved;
        }

        private void HandleItemPlaced(PlacePoint point, Grabbable grabbable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].PlacePoint == point)
                {
                    OnItemStored?.Invoke(slots[i], grabbable);
                    return;
                }
            }
        }

        private void HandleItemRemoved(PlacePoint point, Grabbable grabbable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].PlacePoint == point)
                {
                    OnItemRemoved?.Invoke(slots[i], grabbable);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].PlacePoint != null)
                {
                    slots[i].PlacePoint.OnPlaceEvent -= HandleItemPlaced;
                    slots[i].PlacePoint.OnRemoveEvent -= HandleItemRemoved;
                }
            }
            if (Instance == this) Instance = null;
        }
    }
}
