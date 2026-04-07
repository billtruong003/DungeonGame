// File: Interfaces/ICombat.cs
// Contract: Các interface cho combat system
using System;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Trạng thái combat của entity (dùng cho data/pipeline/query).
    /// Tên ECombatState (E = Enum) để tránh conflict với class CombatState (StateMachine pattern).
    /// </summary>
    public enum ECombatState
    {
        Idle,           // Ngoài chiến đấu
        Combat,         // Đang trong chiến đấu (lock-on)
        Attacking,      // Đang tung đòn
        Blocking,       // Đang block
        HitStun,        // Đang bị choáng từ hit
        Knockback,      // Đang bị đẩy lùi
        Dead            // Chết
    }

    /// <summary>
    /// Data package khi gây damage - chứa mọi thứ cần thiết để tính damage
    /// </summary>
    [Serializable]
    public class DamageInfo
    {
        public float RawDamage;
        public DamageType Type;
        public float CritMultiplier;    // 1.0 nếu không crit
        public bool IsCrit;
        public float KnockbackForce;    // 0 = không knockback
        public Vector3 HitDirection;    // Hướng đánh (để tính knockback)
        public object Source;           // Ai gây damage
        
        // Flags
        public bool IsHeavyAttack;      // Đánh mạnh - phá block
        public bool IsUnblockable;      // Không thể block
        public bool CanParry;           // Có thể parry
    }

    /// <summary>
    /// Kết quả sau khi tính damage (đã qua defense, block, resist...)
    /// </summary>
    [Serializable]
    public class DamageResult
    {
        public float FinalDamage;
        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodged;
        public bool WasCrit;
        public float DamageReduced;     // Lượng damage bị giảm
        public Vector3 KnockbackDirection;
        public float KnockbackForce;
    }

    /// <summary>
    /// Bất kỳ entity nào có thể nhận damage
    /// </summary>
    public interface IDamageable
    {
        float CurrentHP { get; }
        float MaxHP { get; }
        bool IsAlive { get; }
        ECombatState CurrentCombatState { get; }
        
        /// <summary>
        /// Nhận damage. Trả về kết quả sau khi tính defense/block/dodge.
        /// </summary>
        DamageResult TakeDamage(DamageInfo damageInfo);
        
        event Action<DamageResult> OnDamageTaken;
        event Action OnDeath;
    }

    /// <summary>
    /// Entity có thể gây damage
    /// </summary>
    public interface IDamageDealer
    {
        /// <summary>
        /// Tính damage dựa trên stat + vũ khí + buff hiện tại
        /// </summary>
        DamageInfo CalculateDamage(bool isHeavyAttack = false);
        
        event Action<IDamageable, DamageResult> OnDamageDealt;
    }

    /// <summary>
    /// Entity có thể lock-on target
    /// </summary>
    public interface ITargetLockable
    {
        Transform LockOnPoint { get; }  // Điểm để camera/player nhìn vào
        bool CanBeLocked { get; }
    }

    /// <summary>
    /// Interface cho hệ thống lock-on target
    /// </summary>
    public interface ILockOnSystem
    {
        ITargetLockable CurrentTarget { get; }
        bool IsLockedOn { get; }
        
        void LockOn(ITargetLockable target);
        void LockOff();
        void SwitchTarget(int direction); // -1 = trái, 1 = phải
        
        event Action<ITargetLockable> OnTargetLocked;
        event Action OnTargetLost;
    }

    /// <summary>
    /// Entity có thể trang bị vũ khí
    /// </summary>
    public interface IWeaponUser
    {
        IWeapon MainHandWeapon { get; }
        IWeapon OffHandWeapon { get; }  // null nếu không có
        WeaponType CurrentWeaponType { get; }
        
        void EquipWeapon(IWeapon weapon, WeaponSlot slot);
        void UnequipWeapon(WeaponSlot slot);
        
        /// <summary>
        /// Khi equip/unequip, fire event để AnimationController biết switch bộ anim
        /// </summary>
        event Action<IWeapon, WeaponSlot> OnWeaponChanged;
    }
}
