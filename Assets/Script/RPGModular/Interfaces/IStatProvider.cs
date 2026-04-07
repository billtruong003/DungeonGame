// File: Interfaces/IStatProvider.cs
// Contract: Bất kỳ module nào cần đọc chỉ số đều gọi qua interface này
// Combat, Skill, AI đều không cần biết stat được tính như thế nào
using System;

namespace RPGModular
{
    /// <summary>
    /// 7 chỉ số cơ bản của nhân vật.
    /// Mỗi chỉ số có base (từ level/point), bonus (từ equipment/buff), final (tổng).
    /// </summary>
    public enum StatType
    {
        STR,    // Sức mạnh - tăng physical damage, carry weight
        INT,    // Trí tuệ - tăng magic damage, mana pool, magic defense  
        AGI,    // Nhanh nhẹn - tăng attack speed, movement speed, dodge chance
        DEX,    // Khéo léo - tăng accuracy, crit chance, ranged damage
        VIT,    // Thể lực - tăng HP, physical defense, stamina
        LUK,    // May mắn - tăng crit damage, drop rate, rare find
        TECH    // Kỹ thuật - tăng crafting quality, skill effectiveness, parry window
    }

    /// <summary>
    /// Loại damage - quyết định công thức tính và resistance nào áp dụng
    /// </summary>
    public enum DamageType
    {
        // Physical
        Slash,      // Chém - kiếm, rìu
        Pierce,     // Đâm - thương, dagger, mũi tên
        Strike,     // Đánh - tay không, khiên bash, gậy đập
        
        // Magical  
        Fire,
        Ice,
        Lightning,
        Dark,
        Holy
    }

    /// <summary>
    /// Interface chính để đọc stat. Combat, Skill, UI đều gọi qua đây.
    /// </summary>
    public interface IStatProvider
    {
        float GetStat(StatType type);
        float GetBaseStat(StatType type);
        float GetBonusStat(StatType type);
        
        // Derived stats - tính từ các stat cơ bản
        float MaxHP { get; }
        float MaxMana { get; }
        float MaxStamina { get; }
        float PhysicalAttack { get; }
        float MagicAttack { get; }
        float PhysicalDefense { get; }
        float MagicDefense { get; }
        float AttackSpeed { get; }      // Multiplier, 1.0 = bình thường
        float MoveSpeed { get; }        // Base move speed
        float CritChance { get; }       // 0-1
        float CritDamage { get; }       // Multiplier, vd 1.5 = 150%
        float DodgeChance { get; }      // 0-1
        float ParryWindow { get; }      // Seconds - cửa sổ parry
        
        event Action<StatType, float, float> OnStatChanged; // type, oldValue, newValue
    }

    /// <summary>
    /// Interface để modify stat - chỉ Equipment và Buff system mới cần
    /// </summary>
    public interface IStatModifiable : IStatProvider
    {
        void AddModifier(StatModifier modifier);
        void RemoveModifier(StatModifier modifier);
        void SetBaseStat(StatType type, float value);
    }

    /// <summary>
    /// Modifier áp lên stat - dùng cho equipment, buff, debuff, passive skill
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
        public int Priority;        // Thứ tự tính: Flat → PercentAdd → PercentMult
        public object Source;       // Ai/cái gì gây ra modifier này (để remove theo source)

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
        Flat,           // +10 STR
        PercentAdd,     // +10% STR (cộng dồn với các PercentAdd khác)
        PercentMult     // ×1.1 STR (nhân riêng, sau khi cộng Flat + PercentAdd)
    }
}
