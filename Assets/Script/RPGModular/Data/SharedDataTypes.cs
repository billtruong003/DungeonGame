using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [Serializable]
    public class StatBonus
    {
        public StatType stat;
        [BillEnumToggleButtons]
        public ModifierType modType;
        public float value;
    }

    [Serializable]
    public class StatRequirement
    {
        public StatType stat;
        public int requiredValue;
    }

    [Serializable]
    public struct ItemStack
    {
        public ItemData Data;
        public int Quantity;

        public bool IsEmpty => Data == null || Quantity <= 0;
        public static ItemStack Empty => new ItemStack { Data = null, Quantity = 0 };

        public ItemStack(ItemData data, int quantity)
        {
            Data = data;
            Quantity = quantity;
        }
    }

    [Serializable]
    public class SkillPrerequisite
    {
        public SkillData skill;
        public int requiredLevel;
    }

    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        [BillSlider(0f, 1f)]
        public float dropChance = 0.1f;
    }

    [Serializable]
    public class ActiveStatusEffect
    {
        public StatusEffectData Data;
        public float RemainingDuration;
        public int CurrentStacks;
        public float TickTimer;
        public object Source;

        public string EffectId => Data != null ? Data.effectID : "";
    }
}
