// File: Core/Stats/CharacterStats.cs
// Layer 2: Hệ thống chỉ số nhân vật
// Tính toán derived stats từ 7 chỉ số cơ bản
// Support modifier system cho equipment/buff/debuff
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RPGModular
{
    public class CharacterStats : MonoBehaviour, IStatModifiable
    {
        [Header("Base Stats (từ level/point allocation)")]
        [SerializeField] private float baseSTR = 10;
        [SerializeField] private float baseINT = 10;
        [SerializeField] private float baseAGI = 10;
        [SerializeField] private float baseDEX = 10;
        [SerializeField] private float baseVIT = 10;
        [SerializeField] private float baseLUK = 5;
        [SerializeField] private float baseTECH = 5;

        [Header("Derived Stat Formulas (tuning)")]
        [SerializeField] private float hpPerVIT = 15f;
        [SerializeField] private float baseHP = 100f;
        [SerializeField] private float manaPerINT = 12f;
        [SerializeField] private float baseMana = 50f;
        [SerializeField] private float staminaPerVIT = 8f;
        [SerializeField] private float staminaPerAGI = 4f;
        [SerializeField] private float baseStamina = 100f;

        // Internal storage
        private Dictionary<StatType, float> baseStats = new Dictionary<StatType, float>();
        private Dictionary<StatType, List<StatModifier>> modifiers = new Dictionary<StatType, List<StatModifier>>();
        private Dictionary<StatType, float> cachedFinalStats = new Dictionary<StatType, float>();
        private bool isDirty = true;

        public event Action<StatType, float, float> OnStatChanged;

        #region IStatProvider - Đọc chỉ số

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

        // Derived stats
        public float MaxHP => baseHP + GetStat(StatType.VIT) * hpPerVIT;
        public float MaxMana => baseMana + GetStat(StatType.INT) * manaPerINT;
        public float MaxStamina => baseStamina + GetStat(StatType.VIT) * staminaPerVIT 
                                               + GetStat(StatType.AGI) * staminaPerAGI;

        public float PhysicalAttack => GetStat(StatType.STR) * 2f + GetStat(StatType.DEX) * 0.5f;
        public float MagicAttack => GetStat(StatType.INT) * 2.5f;
        
        public float PhysicalDefense => GetStat(StatType.VIT) * 1.5f + GetStat(StatType.STR) * 0.3f;
        public float MagicDefense => GetStat(StatType.INT) * 1.2f + GetStat(StatType.VIT) * 0.5f;

        // Attack speed: AGI chính, DEX phụ. Clamp 0.5 - 2.0
        public float AttackSpeed => Mathf.Clamp(
            1.0f + (GetStat(StatType.AGI) - 10f) * 0.02f + (GetStat(StatType.DEX) - 10f) * 0.01f,
            0.5f, 2.0f);

        // Move speed: AGI chính. Clamp 3-10
        public float MoveSpeed => Mathf.Clamp(
            5.0f + (GetStat(StatType.AGI) - 10f) * 0.15f,
            3.0f, 10.0f);

        // Crit: DEX chính, LUK phụ. Clamp 0-0.75
        public float CritChance => Mathf.Clamp01(
            0.05f + GetStat(StatType.DEX) * 0.005f + GetStat(StatType.LUK) * 0.003f);

        // Crit damage: LUK chính. Min 1.5
        public float CritDamage => 1.5f + GetStat(StatType.LUK) * 0.015f;

        // Dodge: AGI chính, LUK phụ. Clamp 0-0.5
        public float DodgeChance => Mathf.Clamp(
            GetStat(StatType.AGI) * 0.004f + GetStat(StatType.LUK) * 0.002f,
            0f, 0.5f);

        // Parry window: TECH chính. Base 0.15s, max 0.5s
        public float ParryWindow => Mathf.Clamp(
            0.15f + GetStat(StatType.TECH) * 0.005f,
            0.1f, 0.5f);

        #endregion

        #region IStatModifiable - Thay đổi chỉ số

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

        /// <summary>
        /// Remove tất cả modifier từ một source cụ thể.
        /// Dùng khi unequip item hoặc buff hết hạn.
        /// </summary>
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

        #endregion

        #region Internal

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

        /// <summary>
        /// Tính final stat: Base → +Flat → ×(1 + sumPercentAdd) → ×PercentMult1 × PercentMult2...
        /// </summary>
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
                        // Áp dụng sau
                        break;
                }
            }

            // Áp dụng PercentAdd (cộng dồn)
            finalValue *= (1f + sumPercentAdd);

            // Áp dụng PercentMult (nhân riêng từng cái)
            foreach (var mod in modifiers[type].Where(m => m.Type == ModifierType.PercentMult))
            {
                finalValue *= (1f + mod.Value);
            }

            return finalValue;
        }

        #endregion

        #region Debug / Editor

#if UNITY_EDITOR
        [ContextMenu("Log All Stats")]
        private void DebugLogAllStats()
        {
            isDirty = true; // Force recalc
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

        #endregion
    }
}
