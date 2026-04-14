using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Skill Bar", "4 active slots + 2 default (block/parry)")]
    public class SkillBar : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private PlayerSkillBook skillBook;
        [SerializeField] private WeaponHandler weaponHandler;
        [SerializeField] private HealthSystem health;

        [BillBoxGroup("Default Skills")]
        [SerializeField] private SkillData defaultBlockSkill;
        [SerializeField] private SkillData defaultParrySkill;

        public const int ActiveSlotCount = 4;
        public const int DefaultBlockSlot = 4;
        public const int DefaultParrySlot = 5;
        public const int TotalSlots = 6;

        private SkillData[] slots = new SkillData[TotalSlots];
        private float[] cooldownTimers = new float[TotalSlots];

        public event Action<int, SkillData> OnSkillBarChanged;
        public event Action<int, float> OnCooldownUpdate;

        private void Awake()
        {
            if (skillBook == null) skillBook = GetComponent<PlayerSkillBook>();
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();
            if (health == null) health = GetComponent<HealthSystem>();

            // Equip defaults
            slots[DefaultBlockSlot] = defaultBlockSkill;
            slots[DefaultParrySlot] = defaultParrySkill;
        }

        private void Update()
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                if (cooldownTimers[i] > 0)
                {
                    cooldownTimers[i] -= Time.deltaTime;
                    if (cooldownTimers[i] < 0) cooldownTimers[i] = 0;
                    OnCooldownUpdate?.Invoke(i, cooldownTimers[i]);
                }
            }
        }

        public void EquipSkill(SkillData skill, int slot)
        {
            if (slot < 0 || slot >= ActiveSlotCount) return;
            slots[slot] = skill;
            OnSkillBarChanged?.Invoke(slot, skill);
        }

        public SkillData GetSkill(int slot)
        {
            if (slot < 0 || slot >= TotalSlots) return null;
            return slots[slot];
        }

        public bool CanUseSkill(int slot)
        {
            var skill = GetSkill(slot);
            if (skill == null) return false;
            if (cooldownTimers[slot] > 0) return false;

            // Weapon compatibility
            if (skill.requiredWeapons != null && skill.requiredWeapons.Length > 0)
            {
                var currentType = weaponHandler?.CurrentWeaponType ?? WeaponType.Unarmed;
                bool found = false;
                foreach (var w in skill.requiredWeapons)
                    if (w == currentType) { found = true; break; }
                if (!found) return false;
            }

            // Resource check
            if (skill.baseMPCost > 0 && health != null && !health.HasMana(skill.baseMPCost))
                return false;
            if (skill.baseChiCost > 0 && health != null && !health.HasChi(skill.baseChiCost))
                return false;

            return true;
        }

        public float GetCooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= TotalSlots) return 0;
            return cooldownTimers[slot];
        }

        public void StartCooldown(int slot, float duration)
        {
            if (slot < 0 || slot >= TotalSlots) return;
            cooldownTimers[slot] = duration;
        }
    }
}
