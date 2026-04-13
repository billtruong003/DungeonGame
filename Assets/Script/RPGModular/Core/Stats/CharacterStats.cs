using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public class CharacterStats : MonoBehaviour, IStatModifiable
    {
        [BillTitle("Character Stats")]
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseSTR = 10;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseINT = 10;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseAGI = 10;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseDEX = 10;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseVIT = 10;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseLUK = 5;
        [BillBoxGroup("Base Stats")]
        [BillSlider(1, 100)] [SerializeField] private float baseTECH = 5;

        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(1, 50)] [SerializeField] private float hpPerVIT = 15f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(50, 500)] [SerializeField] private float baseHP = 100f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(1, 30)] [SerializeField] private float manaPerINT = 12f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(10, 200)] [SerializeField] private float baseMana = 50f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(1, 20)] [SerializeField] private float staminaPerVIT = 8f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(1, 20)] [SerializeField] private float staminaPerAGI = 4f;
        [BillFoldoutGroup("Derived Stat Formulas")]
        [BillSlider(50, 300)] [SerializeField] private float baseStamina = 100f;

        private Dictionary<StatType, float> baseStats = new Dictionary<StatType, float>();
        private Dictionary<StatType, List<StatModifier>> modifiers = new Dictionary<StatType, List<StatModifier>>();
        private Dictionary<StatType, float> cachedFinalStats = new Dictionary<StatType, float>();
        private bool isDirty = true;

        public event Action<StatType, float, float> OnStatChanged;

        public float GetStat(StatType type)
        {
            if (isDirty) RecalculateAllStats();
            return cachedFinalStats.TryGetValue(type, out float val) ? val : 0f;
        }

        public float GetBaseStat(StatType type)
        {
            return baseStats.TryGetValue(type, out float val) ? val : 0f;
        }

        public float GetBonusStat(StatType type)
        {
            return GetStat(type) - GetBaseStat(type);
        }

        public float MaxHP => baseHP + GetStat(StatType.VIT) * hpPerVIT;
        public float MaxMana => baseMana + GetStat(StatType.INT) * manaPerINT;
        public float MaxStamina => baseStamina + GetStat(StatType.VIT) * staminaPerVIT
                                               + GetStat(StatType.AGI) * staminaPerAGI;

        public float PhysicalAttack => GetStat(StatType.STR) * 2f + GetStat(StatType.DEX) * 0.5f;
        public float MagicAttack => GetStat(StatType.INT) * 2.5f;

        public float PhysicalDefense => GetStat(StatType.VIT) * 1.5f + GetStat(StatType.STR) * 0.3f;
        public float MagicDefense => GetStat(StatType.INT) * 1.2f + GetStat(StatType.VIT) * 0.5f;

        public float AttackSpeed => Mathf.Clamp(
            1.0f + (GetStat(StatType.AGI) - 10f) * 0.02f + (GetStat(StatType.DEX) - 10f) * 0.01f,
            0.5f, 2.0f);

        public float MoveSpeed => Mathf.Clamp(
            5.0f + (GetStat(StatType.AGI) - 10f) * 0.15f,
            3.0f, 10.0f);

        public float CritChance => Mathf.Clamp01(
            0.05f + GetStat(StatType.DEX) * 0.005f + GetStat(StatType.LUK) * 0.003f);

        public float CritDamage => 1.5f + GetStat(StatType.LUK) * 0.015f;

        public float DodgeChance => Mathf.Clamp(
            GetStat(StatType.AGI) * 0.004f + GetStat(StatType.LUK) * 0.002f,
            0f, 0.5f);

        public float ParryWindow => Mathf.Clamp(
            0.15f + GetStat(StatType.TECH) * 0.005f,
            0.1f, 0.5f);

        public void SetBaseStat(StatType type, float value)
        {
            float oldFinal = GetStat(type);
            baseStats[type] = value;
            isDirty = true;
            float newFinal = GetStat(type);

            if (Math.Abs(oldFinal - newFinal) > 0.001f)
                OnStatChanged?.Invoke(type, oldFinal, newFinal);
        }

        public void AddModifier(StatModifier modifier)
        {
            if (!modifiers.ContainsKey(modifier.Stat))
                modifiers[modifier.Stat] = new List<StatModifier>();

            modifiers[modifier.Stat].Add(modifier);
            modifiers[modifier.Stat].Sort((a, b) => a.Priority.CompareTo(b.Priority));

            float oldFinal = cachedFinalStats.TryGetValue(modifier.Stat, out float v) ? v : 0f;
            isDirty = true;
            float newFinal = GetStat(modifier.Stat);

            if (Math.Abs(oldFinal - newFinal) > 0.001f)
                OnStatChanged?.Invoke(modifier.Stat, oldFinal, newFinal);
        }

        public void RemoveModifier(StatModifier modifier)
        {
            if (!modifiers.ContainsKey(modifier.Stat)) return;

            float oldFinal = GetStat(modifier.Stat);
            modifiers[modifier.Stat].Remove(modifier);
            isDirty = true;
            float newFinal = GetStat(modifier.Stat);

            if (Math.Abs(oldFinal - newFinal) > 0.001f)
                OnStatChanged?.Invoke(modifier.Stat, oldFinal, newFinal);
        }

        public void RemoveAllModifiersFromSource(object source)
        {
            foreach (var kvp in modifiers)
            {
                float oldFinal = GetStat(kvp.Key);
                kvp.Value.RemoveAll(m => m.Source == source);
                isDirty = true;
                float newFinal = GetStat(kvp.Key);

                if (Math.Abs(oldFinal - newFinal) > 0.001f)
                    OnStatChanged?.Invoke(kvp.Key, oldFinal, newFinal);
            }
        }

        private void Awake()
        {
            InitializeBaseStats();
        }

        private void InitializeBaseStats()
        {
            baseStats[StatType.STR] = baseSTR;
            baseStats[StatType.INT] = baseINT;
            baseStats[StatType.AGI] = baseAGI;
            baseStats[StatType.DEX] = baseDEX;
            baseStats[StatType.VIT] = baseVIT;
            baseStats[StatType.LUK] = baseLUK;
            baseStats[StatType.TECH] = baseTECH;
            isDirty = true;
        }

        private void RecalculateAllStats()
        {
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                cachedFinalStats[type] = CalculateFinalStat(type);
            }
            isDirty = false;
        }

        private float CalculateFinalStat(StatType type)
        {
            float baseValue = GetBaseStat(type);
            float finalValue = baseValue;

            if (!modifiers.ContainsKey(type) || modifiers[type].Count == 0)
                return finalValue;

            float sumPercentAdd = 0f;

            foreach (var mod in modifiers[type])
            {
                switch (mod.Type)
                {
                    case ModifierType.Flat:
                        finalValue += mod.Value;
                        break;
                    case ModifierType.PercentAdd:
                        sumPercentAdd += mod.Value;
                        break;
                    case ModifierType.PercentMult:
                        break;
                }
            }

            finalValue *= (1f + sumPercentAdd);

            foreach (var mod in modifiers[type].Where(m => m.Type == ModifierType.PercentMult))
            {
                finalValue *= (1f + mod.Value);
            }

            return finalValue;
        }

#if UNITY_EDITOR
        [BillButton("Log All Stats")]
        private void DebugLogAllStats()
        {
            isDirty = true;
            Debug.Log($"=== Character Stats ===");
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                Debug.Log($"{type}: {GetBaseStat(type):F1} (base) + {GetBonusStat(type):F1} (bonus) = {GetStat(type):F1}");
            }
            Debug.Log($"HP: {MaxHP:F0} | Mana: {MaxMana:F0} | Stamina: {MaxStamina:F0}");
            Debug.Log($"P.Atk: {PhysicalAttack:F1} | M.Atk: {MagicAttack:F1}");
            Debug.Log($"P.Def: {PhysicalDefense:F1} | M.Def: {MagicDefense:F1}");
            Debug.Log($"Atk Speed: {AttackSpeed:F2}x | Move Speed: {MoveSpeed:F1}");
            Debug.Log($"Crit: {CritChance:P1} × {CritDamage:F2} | Dodge: {DodgeChance:P1}");
            Debug.Log($"Parry Window: {ParryWindow:F3}s");
        }
#endif

    }
}
