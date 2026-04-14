using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Player Skill Book", "Skill learning and SP management")]
    public class PlayerSkillBook : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private LevelSystem levelSystem;

        private Dictionary<string, int> learnedSkills = new Dictionary<string, int>();
        private List<StatModifier> passiveModifiers = new List<StatModifier>();

        public int AvailableSkillPoints => levelSystem != null ? levelSystem.UnspentSkillPoints : 0;

        public event Action<SkillData, int> OnSkillLearned;
        public event Action<int> OnSkillPointsChanged;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (levelSystem == null) levelSystem = GetComponent<LevelSystem>();
        }

        public int GetSkillLevel(SkillData skill)
        {
            if (skill == null) return 0;
            return learnedSkills.TryGetValue(skill.skillID, out int level) ? level : 0;
        }

        public bool CanLearn(SkillData skill)
        {
            if (skill == null) return false;
            int currentLevel = GetSkillLevel(skill);
            if (currentLevel >= skill.maxLevel) return false;

            // Check SP cost
            int cost = GetSpCost(skill, currentLevel);
            if (AvailableSkillPoints < cost) return false;

            // Check prerequisites
            if (skill.prerequisites != null)
            {
                foreach (var prereq in skill.prerequisites)
                {
                    if (GetSkillLevel(prereq.skill) < prereq.requiredLevel)
                        return false;
                }
            }

            return true;
        }

        public bool LearnOrUpgrade(SkillData skill)
        {
            if (!CanLearn(skill)) return false;

            int currentLevel = GetSkillLevel(skill);
            int cost = GetSpCost(skill, currentLevel);

            // Spend SP via LevelSystem
            for (int i = 0; i < cost; i++)
            {
                if (!levelSystem.SpendSkillPoint()) return false;
            }

            int newLevel = currentLevel + 1;
            learnedSkills[skill.skillID] = newLevel;

            // Apply passive bonuses
            if (skill.category == SkillCategory.Passive && skill.passiveBonuses != null)
            {
                RemovePassiveModifiers(skill);
                ApplyPassiveModifiers(skill, newLevel);
            }

            OnSkillLearned?.Invoke(skill, newLevel);
            OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
            return true;
        }

        public List<SkillData> GetLearnedActiveSkills()
        {
            var result = new List<SkillData>();
            // Need SkillDatabase to resolve IDs → SkillData
            return result;
        }

        public void ResetAllSkills(int goldCost = 5000)
        {
            if (Game.Inv != null && !Game.Inv.SpendGold(goldCost)) return;

            // Refund all SP
            int totalRefund = 0;
            foreach (var kvp in learnedSkills)
                totalRefund += kvp.Value; // simplified: 1 SP per level refund

            for (int i = 0; i < totalRefund; i++)
                levelSystem?.RefundSkillPoint();

            // Remove all passive modifiers
            foreach (var mod in passiveModifiers)
                stats?.RemoveModifier(mod);
            passiveModifiers.Clear();

            learnedSkills.Clear();
            OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
        }

        private int GetSpCost(SkillData skill, int currentLevel)
        {
            if (skill.spCostPerLevel != null && currentLevel < skill.spCostPerLevel.Length)
                return skill.spCostPerLevel[currentLevel];
            return 1;
        }

        private void ApplyPassiveModifiers(SkillData skill, int level)
        {
            if (skill.passiveBonuses == null || stats == null) return;
            foreach (var bonus in skill.passiveBonuses)
            {
                var mod = new StatModifier(bonus.stat, bonus.modType, bonus.value * level, 0, skill);
                stats.AddModifier(mod);
                passiveModifiers.Add(mod);
            }
        }

        private void RemovePassiveModifiers(SkillData skill)
        {
            if (stats == null) return;
            stats.RemoveAllModifiersFromSource(skill);
            passiveModifiers.RemoveAll(m => m.Source == (object)skill);
        }
    }
}
