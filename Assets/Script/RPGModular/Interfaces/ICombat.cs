using System;
using UnityEngine;

namespace RPGModular
{

    public enum ECombatState
    {
        Idle,
        Combat,
        Attacking,
        Blocking,
        Parrying,
        HitStun,
        Knockback,
        Dodge,
        GuardBreak,
        Dead
    }

    [Serializable]
    public class DamageInfo
    {
        public float RawDamage;
        public DamageType Type;
        public float CritMultiplier;
        public bool IsCrit;
        public float KnockbackForce;
        public Vector3 HitDirection;
        public object Source;

        public bool IsHeavyAttack;
        public bool IsUnblockable;
        public bool CanParry;
    }

    [Serializable]
    public class DamageResult
    {
        public float FinalDamage;
        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodged;
        public bool WasCrit;
        public float DamageReduced;
        public Vector3 KnockbackDirection;
        public float KnockbackForce;
    }

    public interface IDamageable
    {
        float CurrentHP { get; }
        float MaxHP { get; }
        bool IsAlive { get; }
        ECombatState CurrentCombatState { get; }

        DamageResult TakeDamage(DamageInfo damageInfo);

        event Action<DamageResult> OnDamageTaken;
        event Action OnDeath;
    }

    public interface IDamageDealer
    {

        DamageInfo CalculateDamage(bool isHeavyAttack = false);

        event Action<IDamageable, DamageResult> OnDamageDealt;
    }

    public interface ITargetLockable
    {
        Transform LockOnPoint { get; }
        bool CanBeLocked { get; }
    }

    public interface ILockOnSystem
    {
        ITargetLockable CurrentTarget { get; }
        bool IsLockedOn { get; }

        void LockOn(ITargetLockable target);
        void LockOff();
        void SwitchTarget(int direction);

        event Action<ITargetLockable> OnTargetLocked;
        event Action OnTargetLost;
    }

    public interface IWeaponUser
    {
        IWeapon MainHandWeapon { get; }
        IWeapon OffHandWeapon { get; }
        WeaponType CurrentWeaponType { get; }

        void EquipWeapon(IWeapon weapon, WeaponSlot slot);
        void UnequipWeapon(WeaponSlot slot);

        event Action<IWeapon, WeaponSlot> OnWeaponChanged;
    }
}
