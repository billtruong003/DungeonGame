// File: Weapons/WeaponHandler.cs
// Component trên nhân vật: quản lý equip/unequip vũ khí
// Khi equip: thêm stat modifier + switch animation set + fire event
// Khi unequip: remove modifier + switch về unarmed
using System;
using UnityEngine;

namespace RPGModular
{
    public class WeaponHandler : MonoBehaviour, IWeaponUser
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private AnimationController animController;

        [Header("Current Equipment")]
        [SerializeField] private WeaponData startingMainHand;
        [SerializeField] private WeaponData startingOffHand;

        // Runtime state
        private IWeapon mainHandWeapon;
        private IWeapon offHandWeapon;
        private StatModifier[] mainHandModifiers;
        private StatModifier[] offHandModifiers;

        // IWeaponUser
        public IWeapon MainHandWeapon => mainHandWeapon;
        public IWeapon OffHandWeapon => offHandWeapon;
        public WeaponType CurrentWeaponType => mainHandWeapon?.Type ?? WeaponType.Unarmed;

        public event Action<IWeapon, WeaponSlot> OnWeaponChanged;

        private void Start()
        {
            // Equip starting weapons
            if (startingMainHand != null)
                EquipWeapon(startingMainHand, WeaponSlot.MainHand);
            if (startingOffHand != null)
                EquipWeapon(startingOffHand, WeaponSlot.OffHand);
            
            // Nếu không có vũ khí, set unarmed anim
            if (mainHandWeapon == null)
            {
                animController?.SetWeaponAnimationSet(
                    WeaponAnimationSet.CreateDefault(WeaponType.Unarmed));
            }
        }

        public void EquipWeapon(IWeapon weapon, WeaponSlot slot)
        {
            // Unequip current first
            UnequipWeapon(slot);

            if (slot == WeaponSlot.MainHand)
            {
                mainHandWeapon = weapon;

                // Thêm stat modifier từ vũ khí
                if (weapon is WeaponData weaponData && stats != null)
                {
                    mainHandModifiers = weaponData.CreateEquipModifiers();
                    foreach (var mod in mainHandModifiers)
                        stats.AddModifier(mod);
                }

                // Switch animation set
                animController?.SetWeaponAnimationSet(weapon.AnimationSet);
            }
            else
            {
                offHandWeapon = weapon;

                if (weapon is WeaponData weaponData && stats != null)
                {
                    offHandModifiers = weaponData.CreateEquipModifiers();
                    foreach (var mod in offHandModifiers)
                        stats.AddModifier(mod);
                }

                // Nếu offhand là khiên, có thể cần merge animation set
                // Ví dụ: Sword + Shield → SwordShield anim set
                UpdateCombinedAnimationSet();
            }

            OnWeaponChanged?.Invoke(weapon, slot);
        }

        public void UnequipWeapon(WeaponSlot slot)
        {
            if (slot == WeaponSlot.MainHand)
            {
                if (mainHandWeapon == null) return;

                // Remove stat modifiers
                if (mainHandModifiers != null && stats != null)
                {
                    foreach (var mod in mainHandModifiers)
                        stats.RemoveModifier(mod);
                    mainHandModifiers = null;
                }

                mainHandWeapon = null;
                
                // Switch về Unarmed anim
                animController?.SetWeaponAnimationSet(
                    WeaponAnimationSet.CreateDefault(WeaponType.Unarmed));
            }
            else
            {
                if (offHandWeapon == null) return;

                if (offHandModifiers != null && stats != null)
                {
                    foreach (var mod in offHandModifiers)
                        stats.RemoveModifier(mod);
                    offHandModifiers = null;
                }

                offHandWeapon = null;
                UpdateCombinedAnimationSet();
            }

            OnWeaponChanged?.Invoke(null, slot);
        }

        /// <summary>
        /// Xử lý combo animation set khi có cả main + off hand.
        /// VD: Sword + Shield → dùng SwordShield anim set
        ///     Dagger + Dagger → dùng DualWield anim set
        /// </summary>
        private void UpdateCombinedAnimationSet()
        {
            if (mainHandWeapon == null) return;

            // Nếu chỉ có main hand, dùng anim của main hand
            if (offHandWeapon == null)
            {
                animController?.SetWeaponAnimationSet(mainHandWeapon.AnimationSet);
                return;
            }

            // Combo detection
            WeaponType mainType = mainHandWeapon.Type;
            WeaponType offType = offHandWeapon.Type;

            // Sword + Shield
            if (offType == WeaponType.Shield)
            {
                // Dùng anim set custom: SwordShield_Idle, SwordShield_Atk1...
                // Hoặc fallback về main hand anim + shield block overlay
                var combinedSet = WeaponAnimationSet.CreateDefault(mainType);
                combinedSet.BlockIdle = "Shield_Block";
                combinedSet.BlockHit = "Shield_Block_Hit";
                combinedSet.BlockBreak = "Shield_Block_Break";
                animController?.SetWeaponAnimationSet(combinedSet);
                return;
            }

            // Dual Wield (2 vũ khí một tay)
            if (IsSingleHandWeapon(mainType) && IsSingleHandWeapon(offType))
            {
                animController?.SetWeaponAnimationSet(
                    WeaponAnimationSet.CreateDefault(WeaponType.DualWield));
                return;
            }

            // Default: dùng main hand anim
            animController?.SetWeaponAnimationSet(mainHandWeapon.AnimationSet);
        }

        private bool IsSingleHandWeapon(WeaponType type)
        {
            return type == WeaponType.Sword || type == WeaponType.Dagger 
                || type == WeaponType.Axe;
        }

        #region Quick Access

        /// <summary>
        /// Lấy animation data cho normal attack hiện tại (dùng cho combo chain)
        /// </summary>
        public AnimationActionData GetNormalAttackAction(int comboIndex)
        {
            var animSet = mainHandWeapon?.AnimationSet 
                         ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);
            
            if (animSet.NormalAttackActions == null || animSet.NormalAttackActions.Length == 0)
                return null;

            int idx = Mathf.Clamp(comboIndex, 0, animSet.NormalAttackActions.Length - 1);
            return animSet.NormalAttackActions[idx];
        }

        /// <summary>
        /// Số combo tối đa của vũ khí hiện tại
        /// </summary>
        public int MaxComboCount
        {
            get
            {
                var animSet = mainHandWeapon?.AnimationSet 
                             ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);
                return animSet.NormalAttackChain?.Length ?? 1;
            }
        }

        #endregion
    }
}
