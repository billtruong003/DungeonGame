// File: Interfaces/IWeapon.cs
// Contract: Combat và Animation chỉ biết vũ khí qua interface này
// Mỗi loại vũ khí implement riêng, plug-and-play
using System;

namespace RPGModular
{
    /// <summary>
    /// Loại vũ khí - quyết định bộ animation và moveset
    /// </summary>
    public enum WeaponType
    {
        Unarmed,        // Tay không
        Sword,          // Kiếm một tay
        Shield,         // Khiên (cũng có thể đánh)
        Spear,          // Thương
        Bow,            // Cung
        Staff,          // Gậy phép
        Dagger,         // Dao ngắn
        DualWield,      // Song thủ (2 vũ khí một tay)
        GreatSword,     // Kiếm hai tay
        Axe             // Rìu
    }

    /// <summary>
    /// Slot trang bị vũ khí
    /// </summary>
    public enum WeaponSlot
    {
        MainHand,
        OffHand
    }

    /// <summary>
    /// Nhóm damage vật lý - quyết định skill nào dùng được
    /// </summary>
    public enum PhysicalDamageGroup
    {
        Sharp,      // Vũ khí sắc nhọn (đâm): Spear, Dagger, mũi tên
        Slash,      // Vũ khí chém: Sword, GreatSword, Axe
        Ranged,     // Vũ khí đánh xa: Bow
        Blunt       // Vũ khí đánh: Shield bash, Staff thường, Unarmed
    }

    /// <summary>
    /// Interface chính cho vũ khí. 
    /// Combat system chỉ cần gọi: "vũ khí này damage bao nhiêu, animation gì, type gì?"
    /// </summary>
    public interface IWeapon
    {
        string WeaponName { get; }
        WeaponType Type { get; }
        WeaponSlot Slot { get; }
        DamageType PrimaryDamageType { get; }
        PhysicalDamageGroup DamageGroup { get; }
        
        // Stat ảnh hưởng
        float BaseDamage { get; }
        float AttackRange { get; }
        float AttackSpeedModifier { get; }   // 1.0 = bình thường, >1 nhanh hơn
        
        // Animation set - mỗi vũ khí trả về tên animation tương ứng
        WeaponAnimationSet AnimationSet { get; }
    }

    /// <summary>
    /// Bộ animation cho một loại vũ khí.
    /// Đây là KEY DESIGN: mỗi vũ khí tự định nghĩa bộ anim của nó.
    /// AnimationController chỉ cần gọi tên, không cần biết vũ khí gì.
    /// </summary>
    [Serializable]
    public class WeaponAnimationSet
    {
        // Idle & Locomotion trong combat
        public string CombatIdle;           // vd: "Sword_Idle", "Bow_Idle", "Unarmed_Idle"
        public string CombatWalkForward;    // vd: "Sword_Walk_Fwd"
        public string CombatWalkBackward;   // vd: "Sword_Walk_Back"  
        public string CombatWalkLeft;
        public string CombatWalkRight;

        // Normal attacks (combo chain)
        public string[] NormalAttackChain;  // vd: ["Sword_Atk1", "Sword_Atk2", "Sword_Atk3"]
        
        // Action data cho mỗi normal attack (timing phase)
        public AnimationActionData[] NormalAttackActions;

        // Block
        public string BlockIdle;            // vd: "Shield_Block_Idle"
        public string BlockHit;             // vd: "Shield_Block_Impact"
        public string BlockBreak;           // vd: "Shield_Block_Break" (đẩy lùi)

        // Hit reactions
        public string HitLight;             // Bị đánh nhẹ
        public string HitHeavy;             // Bị đánh mạnh
        public string Knockback;            // Bị đẩy lùi

        // Equip/Unequip
        public string Equip;                // Rút vũ khí ra
        public string Unequip;              // Cất vũ khí

        /// <summary>
        /// Tạo default animation set cho một weapon type.
        /// Convention: {WeaponType}_{Action}
        /// VD: Sword_Idle, Sword_Atk1, Bow_Idle, Unarmed_Atk1
        /// </summary>
        public static WeaponAnimationSet CreateDefault(WeaponType type)
        {
            string prefix = type.ToString();
            return new WeaponAnimationSet
            {
                CombatIdle = $"{prefix}_Idle",
                CombatWalkForward = $"{prefix}_Walk_Fwd",
                CombatWalkBackward = $"{prefix}_Walk_Back",
                CombatWalkLeft = $"{prefix}_Walk_Left",
                CombatWalkRight = $"{prefix}_Walk_Right",
                
                NormalAttackChain = new[] { $"{prefix}_Atk1", $"{prefix}_Atk2", $"{prefix}_Atk3" },
                NormalAttackActions = new[]
                {
                    new AnimationActionData 
                    { 
                        AnimationStateName = $"{prefix}_Atk1",
                        StartupEnd = 0.15f, ActiveEnd = 0.5f, 
                        CanCancelStartup = true, CanCancelRecovery = true 
                    },
                    new AnimationActionData 
                    { 
                        AnimationStateName = $"{prefix}_Atk2",
                        StartupEnd = 0.2f, ActiveEnd = 0.55f,
                        CanCancelStartup = true, CanCancelRecovery = true
                    },
                    new AnimationActionData 
                    { 
                        AnimationStateName = $"{prefix}_Atk3",
                        StartupEnd = 0.25f, ActiveEnd = 0.65f,
                        CanCancelStartup = true, CanCancelRecovery = false // Hit cuối không cancel được
                    }
                },
                
                BlockIdle = $"{prefix}_Block",
                BlockHit = $"{prefix}_Block_Hit",
                BlockBreak = $"{prefix}_Block_Break",
                
                HitLight = $"{prefix}_Hit_Light",
                HitHeavy = $"{prefix}_Hit_Heavy",
                Knockback = $"{prefix}_Knockback",
                
                Equip = $"{prefix}_Equip",
                Unequip = $"{prefix}_Unequip"
            };
        }
    }
}
