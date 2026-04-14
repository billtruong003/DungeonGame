using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Equipment System", "8-slot equipment management")]
    public class EquipmentSystem : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private Inventory inventory;
        [SerializeField] private WeaponHandler weaponHandler;

        // 8 equipment slots
        private ItemData[] equipped;
        private StatModifier[][] appliedModifiers;

        public event Action<EquipSlot, ItemData> OnEquipped;
        public event Action<EquipSlot, ItemData> OnUnequipped;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();

            int slotCount = Enum.GetValues(typeof(EquipSlot)).Length;
            equipped = new ItemData[slotCount];
            appliedModifiers = new StatModifier[slotCount][];
        }

        public ItemData GetEquipped(EquipSlot slot) => equipped[(int)slot];
        public bool IsSlotEmpty(EquipSlot slot) => equipped[(int)slot] == null;

        /// <summary>Equip item. Returns old item (null if slot was empty).</summary>
        public ItemData Equip(ItemData item, EquipSlot slot)
        {
            if (item == null || !item.isEquippable) return null;

            // Check stat requirements
            if (item.requirements != null)
            {
                foreach (var req in item.requirements)
                {
                    if (stats.GetStat(req.stat) < req.requiredValue)
                        return null; // requirement not met
                }
            }

            // Unequip old
            ItemData oldItem = Unequip(slot);

            // Set new
            int idx = (int)slot;
            equipped[idx] = item;

            // Apply stat modifiers
            if (item.equipBonuses != null && item.equipBonuses.Length > 0)
            {
                appliedModifiers[idx] = new StatModifier[item.equipBonuses.Length];
                for (int i = 0; i < item.equipBonuses.Length; i++)
                {
                    var bonus = item.equipBonuses[i];
                    var mod = new StatModifier(bonus.stat, bonus.modType, bonus.value, 0, this);
                    appliedModifiers[idx][i] = mod;
                    stats.AddModifier(mod);
                }
            }

            // Weapon sync
            if (slot == EquipSlot.MainHand || slot == EquipSlot.OffHand)
            {
                // WeaponHandler handles weapon-specific logic
                // TODO: bridge ItemData weapon to IWeapon and call weaponHandler.EquipWeapon()
            }

            // Remove from inventory
            inventory?.RemoveItem(item, 1);

            OnEquipped?.Invoke(slot, item);
            return oldItem;
        }

        /// <summary>Unequip slot. Returns removed item, adds to inventory.</summary>
        public ItemData Unequip(EquipSlot slot)
        {
            int idx = (int)slot;
            ItemData oldItem = equipped[idx];
            if (oldItem == null) return null;

            // Remove stat modifiers
            if (appliedModifiers[idx] != null)
            {
                foreach (var mod in appliedModifiers[idx])
                    stats.RemoveModifier(mod);
                appliedModifiers[idx] = null;
            }

            equipped[idx] = null;

            // Return to inventory
            inventory?.AddItem(oldItem, 1);

            OnUnequipped?.Invoke(slot, oldItem);
            return oldItem;
        }

        public float GetTotalArmorDefense()
        {
            float total = 0f;
            for (int i = 0; i < equipped.Length; i++)
            {
                if (equipped[i] == null) continue;
                if (equipped[i].equipBonuses == null) continue;
                foreach (var b in equipped[i].equipBonuses)
                {
                    if (b.stat == StatType.VIT && b.modType == ModifierType.Flat)
                        total += b.value;
                }
            }
            return total;
        }
    }
}
