using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Level System", "EXP, levels, stat/skill points")]
    public class LevelSystem : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(1, 200)]
        [SerializeField] private int maxLevel = 50;
        [BillBoxGroup("Config")]
        [BillSlider(1, 10)]
        [SerializeField] private int statPointsPerLevel = 5;
        [BillBoxGroup("Config")]
        [BillSlider(0, 3)]
        [SerializeField] private int skillPointsPerLevel = 1;

        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;

        [BillReadOnly, BillShowInInspector, BillBoxGroup("Runtime")]
        public int Level { get; private set; } = 1;
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Runtime")]
        public float CurrentExp { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Runtime")]
        public int UnspentStatPoints { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Runtime")]
        public int UnspentSkillPoints { get; private set; }

        public int MaxLevel => maxLevel;
        public float ExpToNextLevel => CalculateExpRequired(Level);
        public float ExpProgress => ExpToNextLevel > 0 ? CurrentExp / ExpToNextLevel : 0f;

        public event Action<int> OnLevelUp;
        public event Action<float, float> OnExpGained;
        public event Action<StatType> OnStatPointSpent;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
        }

        public void AddExp(float amount)
        {
            if (amount <= 0 || Level >= maxLevel) return;

            CurrentExp += amount;
            OnExpGained?.Invoke(amount, CurrentExp);

            // Multi-level-up support
            while (CurrentExp >= ExpToNextLevel && Level < maxLevel)
            {
                CurrentExp -= ExpToNextLevel;
                Level++;
                UnspentStatPoints += statPointsPerLevel;
                UnspentSkillPoints += skillPointsPerLevel;
                OnLevelUp?.Invoke(Level);
            }

            if (Level >= maxLevel)
                CurrentExp = 0f;
        }

        public bool SpendStatPoint(StatType type)
        {
            if (UnspentStatPoints <= 0) return false;
            UnspentStatPoints--;
            stats?.SetBaseStat(type, stats.GetBaseStat(type) + 1f);
            OnStatPointSpent?.Invoke(type);
            return true;
        }

        public bool SpendSkillPoint()
        {
            if (UnspentSkillPoints <= 0) return false;
            UnspentSkillPoints--;
            return true;
        }

        public void RefundSkillPoint()
        {
            UnspentSkillPoints++;
        }

        /// <summary>EXP formula: floor(100 * level^1.5)</summary>
        public static float CalculateExpRequired(int level)
        {
            return Mathf.Floor(100f * Mathf.Pow(level, 1.5f));
        }

        public void SetMaxLevel(int newMax)
        {
            maxLevel = newMax;
        }
    }
}
