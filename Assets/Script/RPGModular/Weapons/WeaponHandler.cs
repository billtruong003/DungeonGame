using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public class WeaponHandler : MonoBehaviour, IWeaponUser
    {
        [BillTitle("Weapon Handler")]
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [BillBoxGroup("Dependencies")]
        [SerializeField] private AnimationController animController;

        [BillBoxGroup("Starting Weapons")]
        [BillInlineEditor]
        [SerializeField] private WeaponData startingMainHand;
        [BillBoxGroup("Starting Weapons")]
        [BillInlineEditor]
        [SerializeField] private WeaponData startingOffHand;

        [BillFoldoutGroup("Weapon Mount Points")]
        [BillInfoBox("Assign hand bone transforms for visual weapon spawning. Leave empty to skip visuals.")]
        [BillRequired("Assign main hand slot for weapon visuals")]
        [SerializeField] private Transform mainHandSlot;
        [BillFoldoutGroup("Weapon Mount Points")]
        [SerializeField] private Transform offHandSlot;
        [BillFoldoutGroup("Weapon Mount Points")]
        [SerializeField] private Transform mainHandSheath;
        [BillFoldoutGroup("Weapon Mount Points")]
        [SerializeField] private Transform offHandSheath;

        private IWeapon mainHandWeapon;
        private IWeapon offHandWeapon;
        private StatModifier[] mainHandModifiers;
        private StatModifier[] offHandModifiers;

        // Visual weapon instances
        private GameObject mainHandVisual;
        private GameObject offHandVisual;

        public IWeapon MainHandWeapon => mainHandWeapon;
        public IWeapon OffHandWeapon => offHandWeapon;
        public WeaponType CurrentWeaponType => mainHandWeapon?.Type ?? WeaponType.Unarmed;

        /// <summary>Access underlying WeaponData SO (for visual prefab, localization, etc.)</summary>
        public WeaponData MainHandWeaponData => mainHandWeapon as WeaponData;
        public WeaponData OffHandWeaponData => offHandWeapon as WeaponData;

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
                SpawnWeaponVisual(weapon, WeaponSlot.MainHand);
            }
            else
            {
                offHandWeapon = weapon;
                ApplyWeaponModifiers(weapon, ref offHandModifiers);
                UpdateCombinedAnimationSet();
                SpawnWeaponVisual(weapon, WeaponSlot.OffHand);
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
                DestroyWeaponVisual(ref mainHandVisual);
            }
            else
            {
                if (offHandWeapon == null) return;
                RemoveWeaponModifiers(ref offHandModifiers);
                offHandWeapon = null;
                UpdateCombinedAnimationSet();
                DestroyWeaponVisual(ref offHandVisual);
            }

            OnWeaponChanged?.Invoke(null, slot);
        }

        // ═══════════════════════════════════════════════════════
        // Visual weapon management
        // ═══════════════════════════════════════════════════════

        private void SpawnWeaponVisual(IWeapon weapon, WeaponSlot slot)
        {
            if (weapon is not WeaponData weaponData) return;

            GameObject prefab = weaponData.WeaponPrefab;
            if (prefab == null) return;

            Transform parent = slot == WeaponSlot.MainHand ? mainHandSlot : offHandSlot;
            if (parent == null) return;

            GameObject visual = Instantiate(prefab, parent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            if (slot == WeaponSlot.MainHand)
                mainHandVisual = visual;
            else
                offHandVisual = visual;
        }

        private void DestroyWeaponVisual(ref GameObject visual)
        {
            if (visual != null)
            {
                Destroy(visual);
                visual = null;
            }
        }

        /// <summary>
        /// Move weapon visual to sheath position (for exploration mode).
        /// </summary>
        public void SheathWeapons()
        {
            MoveVisualToSlot(mainHandVisual, mainHandSheath);
            MoveVisualToSlot(offHandVisual, offHandSheath);
        }

        /// <summary>
        /// Move weapon visual to hand position (for combat mode).
        /// </summary>
        public void UnsheathWeapons()
        {
            MoveVisualToSlot(mainHandVisual, mainHandSlot);
            MoveVisualToSlot(offHandVisual, offHandSlot);
        }

        private void MoveVisualToSlot(GameObject visual, Transform slot)
        {
            if (visual == null || slot == null) return;
            visual.transform.SetParent(slot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }

        // ═══════════════════════════════════════════════════════
        // Stat modifiers
        // ═══════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════
        // Animation set
        // ═══════════════════════════════════════════════════════

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
