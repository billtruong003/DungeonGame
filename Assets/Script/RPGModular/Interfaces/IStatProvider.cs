using System;

namespace RPGModular
{

    public enum StatType
    {
        STR,
        INT,
        AGI,
        DEX,
        VIT,
        LUK,
        TECH
    }

    public enum DamageType
    {

        Slash,
        Pierce,
        Strike,

        Fire,
        Ice,
        Lightning,
        Dark,
        Holy
    }

    public interface IStatProvider
    {
        float GetStat(StatType type);
        float GetBaseStat(StatType type);
        float GetBonusStat(StatType type);

        float MaxHP { get; }
        float MaxMana { get; }
        float MaxStamina { get; }
        float PhysicalAttack { get; }
        float MagicAttack { get; }
        float PhysicalDefense { get; }
        float MagicDefense { get; }
        float AttackSpeed { get; }
        float MoveSpeed { get; }
        float CritChance { get; }
        float CritDamage { get; }
        float DodgeChance { get; }
        float ParryWindow { get; }

        event Action<StatType, float, float> OnStatChanged;
    }

    public interface IStatModifiable : IStatProvider
    {
        void AddModifier(StatModifier modifier);
        void RemoveModifier(StatModifier modifier);
        void SetBaseStat(StatType type, float value);
    }

    [Serializable]
    public class StatModifier
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
        public int Priority;
        public object Source;

        public StatModifier(StatType stat, ModifierType type, float value, int priority = 0, object source = null)
        {
            Stat = stat;
            Type = type;
            Value = value;
            Priority = priority;
            Source = source;
        }
    }

    public enum ModifierType
    {
        Flat,
        PercentAdd,
        PercentMult
    }
}
