using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Skill Caster", "Execute skill cast flow")]
    public class SkillCaster : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private SkillBar skillBar;
        [SerializeField] private PlayerSkillBook skillBook;
        [SerializeField] private HealthSystem health;
        [SerializeField] private CombatStateMachine combatSM;

        public SkillData CurrentSkill { get; private set; }
        public int CurrentSkillLevel { get; private set; }

        public event Action<SkillData> OnSkillCastStart;
        public event Action<SkillData> OnSkillCastComplete;
        public event Action<SkillData> OnSkillCastInterrupted;
        public event Action<SkillData> OnSkillCastFailed;

        private void Awake()
        {
            if (skillBar == null) skillBar = GetComponent<SkillBar>();
            if (skillBook == null) skillBook = GetComponent<PlayerSkillBook>();
            if (health == null) health = GetComponent<HealthSystem>();
            if (combatSM == null) combatSM = GetComponent<CombatStateMachine>();
        }

        /// <summary>Cast skill in slot. Returns true if cast started.</summary>
        public bool Cast(int slotIndex)
        {
            if (skillBar == null) return false;

            var skill = skillBar.GetSkill(slotIndex);
            if (skill == null) return false;

            if (!skillBar.CanUseSkill(slotIndex))
            {
                OnSkillCastFailed?.Invoke(skill);
                return false;
            }

            int level = skillBook != null ? skillBook.GetSkillLevel(skill) : 1;
            if (level <= 0 && slotIndex < SkillBar.ActiveSlotCount)
            {
                // Not learned (default skills bypass this)
                OnSkillCastFailed?.Invoke(skill);
                return false;
            }
            if (level <= 0) level = 1; // default skills = level 1

            // Consume resources
            if (skill.baseMPCost > 0) health?.TryConsumeMana(skill.baseMPCost);
            if (skill.baseChiCost > 0) health?.TryConsumeChi(skill.baseChiCost);

            // Start cooldown
            skillBar.StartCooldown(slotIndex, skill.cooldown);

            // Store current
            CurrentSkill = skill;
            CurrentSkillLevel = level;

            // Switch combat state
            if (skill.castTime > 0 && combatSM != null)
            {
                combatSM.SwitchState(
                    new SkillChargeState(combatSM, this, skill, level),
                    CombatStateType.SkillCharge);
            }
            else if (combatSM != null)
            {
                combatSM.SwitchState(
                    new SkillExecuteState(combatSM, this, skill, level),
                    CombatStateType.SkillExecute);
            }

            OnSkillCastStart?.Invoke(skill);
            return true;
        }

        public void NotifyCastComplete(SkillData skill)
        {
            OnSkillCastComplete?.Invoke(skill);
            CurrentSkill = null;
        }

        public void NotifyCastInterrupted(SkillData skill)
        {
            OnSkillCastInterrupted?.Invoke(skill);
            CurrentSkill = null;
        }

        /// <summary>Calculate skill damage.</summary>
        public float CalculateSkillDamage(SkillData skill, int level)
        {
            float power = skill.basePower + skill.powerPerLevel * (level - 1);
            float statDmg = skill.scaleType == DamageScaleType.Physical
                ? (Game.Stats?.PhysicalAttack ?? 0f)
                : (Game.Stats?.MagicAttack ?? 0f);

            float scaling = (Game.Stats?.GetStat(skill.primaryScalingStat) ?? 0f) * skill.scalingRatio;
            float weaponATK = Game.Weapon?.MainHandWeapon?.BaseDamage ?? 5f;
            float comboBonus = Game.Combo?.GetComboDamageBonus() ?? 1f;
            float focusBonus = Game.Focus?.GetDamageBonus() ?? 1f;

            float rawDamage = (statDmg + weaponATK + scaling) * (power / 100f) * comboBonus * focusBonus;
            return rawDamage;
        }
    }
}
