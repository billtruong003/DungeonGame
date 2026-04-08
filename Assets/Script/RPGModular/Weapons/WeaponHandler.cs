using System;
using UnityEngine;

namespace RPGModular
{
    public class WeaponHandler : MonoBehaviour, IWeaponUser
    {
        [SerializeField] private CharacterStats stats;
        [SerializeField] private AnimationController animController;
        [SerializeField] private WeaponData startingMainHand;
        [SerializeField] private WeaponData startingOffHand;

        private IWeapon mainHandWeapon;
        private IWeapon offHandWeapon;
        private StatModifier[] mainHandModifiers;
        private StatModifier[] offHandModifiers;

        public IWeapon MainHandWeapon => mainHandWeapon;
        public IWeapon OffHandWeapon => offHandWeapon;
        public WeaponType CurrentWeaponType => mainHandWeapon?.Type ?? WeaponType.Unarmed;

        public event Action<IWeapon, WeaponSlot> OnWeaponChanged;

        private void Start()
        {
            if (startingMainHand != null)
                EquipWeapon(startingMainHand, WeaponSlot.MainHand);
            if (startingOffHand != null)
                EquipWeapon(startingOffHand, WeaponSlot.OffHand);

            if (mainHandWeapon == null)
                animController?.SetWeaponAnimationSet(WeaponAnimationSet.CreateDefault(WeaponType.Unarmed));
        }

        public void EquipWeapon(IWeapon weapon, WeaponSlot slot)
        {
            UnequipWeapon(slot);

            if (slot == WeaponSlot.MainHand)
            {
                mainHandWeapon = weapon;
                ApplyWeaponModifiers(weapon, ref mainHandModifiers);
                animController?.SetWeaponAnimationSet(weapon.AnimationSet);
            }
            else
            {
                offHandWeapon = weapon;
                ApplyWeaponModifiers(weapon, ref offHandModifiers);
                UpdateCombinedAnimationSet();
            }

            OnWeaponChanged?.Invoke(weapon, slot);
        }

        public void UnequipWeapon(WeaponSlot slot)
        {
            if (slot == WeaponSlot.MainHand)
            {
                if (mainHandWeapon == null) return;
                RemoveWeaponModifiers(ref mainHandModifiers);
                mainHandWeapon = null;
                animController?.SetWeaponAnimationSet(WeaponAnimationSet.CreateDefault(WeaponType.Unarmed));
            }
            else
            {
                if (offHandWeapon == null) return;
                RemoveWeaponModifiers(ref offHandModifiers);
                offHandWeapon = null;
                UpdateCombinedAnimationSet();
            }

            OnWeaponChanged?.Invoke(null, slot);
        }

        private void ApplyWeaponModifiers(IWeapon weapon, ref StatModifier[] modifiers)
        {
            if (weapon is not WeaponData weaponData || stats == null) return;

            modifiers = weaponData.CreateEquipModifiers();
            foreach (var mod in modifiers)
                stats.AddModifier(mod);
        }

        private void RemoveWeaponModifiers(ref StatModifier[] modifiers)
        {
            if (modifiers == null || stats == null) return;

            foreach (var mod in modifiers)
                stats.RemoveModifier(mod);
            modifiers = null;
        }

        private void UpdateCombinedAnimationSet()
        {
            if (mainHandWeapon == null) return;

            if (offHandWeapon == null)
            {
                animController?.SetWeaponAnimationSet(mainHandWeapon.AnimationSet);
                return;
            }

            WeaponType offType = offHandWeapon.Type;

            if (offType == WeaponType.Shield)
            {
                var combinedSet = WeaponAnimationSet.CreateDefault(mainHandWeapon.Type);
                combinedSet.BlockIdle = "Shield_Block";
                combinedSet.BlockHit = "Shield_Block_Hit";
                combinedSet.BlockBreak = "Shield_Block_Break";
                animController?.SetWeaponAnimationSet(combinedSet);
                return;
            }

            if (offType == WeaponType.Dagger)
            {
                animController?.SetWeaponAnimationSet(mainHandWeapon.AnimationSet);
                return;
            }

            if (IsSingleHandWeapon(mainHandWeapon.Type) && IsSingleHandWeapon(offType))
            {
                animController?.SetWeaponAnimationSet(WeaponAnimationSet.CreateDefault(WeaponType.DualWield));
                return;
            }

            animController?.SetWeaponAnimationSet(mainHandWeapon.AnimationSet);
        }

        private bool IsSingleHandWeapon(WeaponType type)
        {
            return type == WeaponType.Sword
                || type == WeaponType.Dagger
                || type == WeaponType.Axe
                || type == WeaponType.Katana;
        }

        public AnimationActionData GetNormalAttackAction(int comboIndex)
        {
            var animSet = mainHandWeapon?.AnimationSet
                ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            if (animSet.NormalAttackActions == null || animSet.NormalAttackActions.Length == 0)
                return null;

            int idx = Mathf.Clamp(comboIndex, 0, animSet.NormalAttackActions.Length - 1);
            return animSet.NormalAttackActions[idx];
        }

        public int MaxComboCount
        {
            get
            {
                var animSet = mainHandWeapon?.AnimationSet
                    ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);
                return animSet.NormalAttackChain?.Length ?? 1;
            }
        }
    }
}
