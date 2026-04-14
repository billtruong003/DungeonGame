using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Inventory", "Player item storage")]
    public class Inventory : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(10, 100)]
        [SerializeField] private int maxSlots = 30;

        [BillReadOnly, BillShowInInspector]
        public int Gold { get; private set; }

        private ItemStack[] slots;

        public int MaxSlots => maxSlots;
        public int UsedSlots { get { int c = 0; for (int i = 0; i < slots.Length; i++) if (!slots[i].IsEmpty) c++; return c; } }

        public event Action<int, ItemStack> OnSlotChanged;
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action<int> OnGoldChanged;
        public event Action OnInventoryFull;

        private void Awake()
        {
            slots = new ItemStack[maxSlots];
            for (int i = 0; i < maxSlots; i++)
                slots[i] = ItemStack.Empty;
        }

        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return ItemStack.Empty;
            return slots[index];
        }

        /// <summary>Add items. Returns overflow count (items that didn't fit).</summary>
        public int AddItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return quantity;
            int remaining = quantity;

            // Stack into existing slots first
            if (item.maxStack > 1)
            {
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                {
                    if (slots[i].Data == item && slots[i].Quantity < item.maxStack)
                    {
                        int canAdd = Mathf.Min(remaining, item.maxStack - slots[i].Quantity);
                        slots[i] = new ItemStack(item, slots[i].Quantity + canAdd);
                        remaining -= canAdd;
                        OnSlotChanged?.Invoke(i, slots[i]);
                    }
                }
            }

            // Fill empty slots
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int canAdd = Mathf.Min(remaining, item.maxStack);
                    slots[i] = new ItemStack(item, canAdd);
                    remaining -= canAdd;
                    OnSlotChanged?.Invoke(i, slots[i]);
                }
            }

            int added = quantity - remaining;
            if (added > 0) OnItemAdded?.Invoke(item, added);
            if (remaining > 0) OnInventoryFull?.Invoke();

            return remaining;
        }

        /// <summary>Remove items. Returns count actually removed.</summary>
        public int RemoveItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return 0;
            int remaining = quantity;

            for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                if (slots[i].Data == item)
                {
                    int canRemove = Mathf.Min(remaining, slots[i].Quantity);
                    int newQty = slots[i].Quantity - canRemove;
                    slots[i] = newQty > 0 ? new ItemStack(item, newQty) : ItemStack.Empty;
                    remaining -= canRemove;
                    OnSlotChanged?.Invoke(i, slots[i]);
                }
            }

            int removed = quantity - remaining;
            if (removed > 0) OnItemRemoved?.Invoke(item, removed);
            return removed;
        }

        public bool HasItem(ItemData item, int count = 1) => GetItemCount(item) >= count;

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Data == item)
                    total += slots[i].Quantity;
            return total;
        }

        public bool UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            var stack = slots[slotIndex];
            if (stack.IsEmpty || stack.Data.type != ItemType.Consumable) return false;

            var item = stack.Data;
            var health = Game.Health;
            if (health == null) return false;

            if (item.healAmount > 0) health.Heal(item.healAmount);
            if (item.manaAmount > 0) health.ModifyResource(ResourceType.Mana, item.manaAmount);
            if (item.staminaAmount > 0) health.ModifyResource(ResourceType.Stamina, item.staminaAmount);
            if (item.chiAmount > 0) health.ModifyChi(item.chiAmount);

            int newQty = stack.Quantity - 1;
            slots[slotIndex] = newQty > 0 ? new ItemStack(item, newQty) : ItemStack.Empty;
            OnSlotChanged?.Invoke(slotIndex, slots[slotIndex]);
            OnItemRemoved?.Invoke(item, 1);
            return true;
        }

        public void SwapSlots(int a, int b)
        {
            if (a < 0 || a >= slots.Length || b < 0 || b >= slots.Length || a == b) return;
            (slots[a], slots[b]) = (slots[b], slots[a]);
            OnSlotChanged?.Invoke(a, slots[a]);
            OnSlotChanged?.Invoke(b, slots[b]);
        }

        public void SortByType()
        {
            var items = new List<ItemStack>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty) items.Add(slots[i]);
                slots[i] = ItemStack.Empty;
            }
            items.Sort((a, b) => a.Data.type.CompareTo(b.Data.type));
            for (int i = 0; i < items.Count; i++)
            {
                slots[i] = items[i];
                OnSlotChanged?.Invoke(i, slots[i]);
            }
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        public void ExpandSlots(int additionalSlots)
        {
            int newMax = maxSlots + additionalSlots;
            var newSlots = new ItemStack[newMax];
            Array.Copy(slots, newSlots, slots.Length);
            for (int i = slots.Length; i < newMax; i++)
                newSlots[i] = ItemStack.Empty;
            slots = newSlots;
            maxSlots = newMax;
        }
    }
}
