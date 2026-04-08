using System;

namespace RPGModular
{
    public enum WeaponType
    {
        Unarmed,
        Sword,
        GreatSword,
        Shield,
        Spear,
        Halberd,
        Bow,
        Bowgun,
        Staff,
        MagicDevice,
        Dagger,
        Knuckle,
        Katana,
        DualWield,
        Axe
    }

    public enum WeaponSlot
    {
        MainHand,
        OffHand
    }

    public enum PhysicalDamageGroup
    {
        Sharp,
        Slash,
        Ranged,
        Blunt
    }

    public interface IWeapon
    {
        string WeaponName { get; }
        WeaponType Type { get; }
        WeaponSlot Slot { get; }
        DamageType PrimaryDamageType { get; }
        PhysicalDamageGroup DamageGroup { get; }
        float BaseDamage { get; }
        float AttackRange { get; }
        float AttackSpeedModifier { get; }
        WeaponAnimationSet AnimationSet { get; }
    }

    [Serializable]
    public class WeaponAnimationSet
    {
        public string CombatIdle;
        public string CombatWalkForward;
        public string CombatWalkBackward;
        public string CombatWalkLeft;
        public string CombatWalkRight;

        public string[] NormalAttackChain;
        public AnimationActionData[] NormalAttackActions;

        public string BlockIdle;
        public string BlockHit;
        public string BlockBreak;

        public string HitLight;
        public string HitHeavy;
        public string Knockback;

        public string Equip;
        public string Unequip;

        public static WeaponAnimationSet CreateDefault(WeaponType type)
        {
            string prefix = type.ToString();
            var config = GetWeaponConfig(type);

            var attackChain = new string[config.ComboLength];
            var attackActions = new AnimationActionData[config.ComboLength];

            for (int i = 0; i < config.ComboLength; i++)
            {
                attackChain[i] = $"{prefix}_Atk{i + 1}";
                attackActions[i] = new AnimationActionData
                {
                    AnimationStateName = $"{prefix}_Atk{i + 1}",
                    StartupEnd = config.StartupEnd,
                    ActiveEnd = config.ActiveEnd,
                    CanCancelStartup = true,
                    CanCancelRecovery = i < config.ComboLength - 1
                };
            }

            return new WeaponAnimationSet
            {
                CombatIdle = $"{prefix}_Idle",
                CombatWalkForward = $"{prefix}_Walk_Fwd",
                CombatWalkBackward = $"{prefix}_Walk_Back",
                CombatWalkLeft = $"{prefix}_Walk_Left",
                CombatWalkRight = $"{prefix}_Walk_Right",

                NormalAttackChain = attackChain,
                NormalAttackActions = attackActions,

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

        private static WeaponComboConfig GetWeaponConfig(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Unarmed:
                    return new WeaponComboConfig(3, 0.12f, 0.45f);
                case WeaponType.Sword:
                    return new WeaponComboConfig(3, 0.15f, 0.50f);
                case WeaponType.GreatSword:
                    return new WeaponComboConfig(3, 0.25f, 0.60f);
                case WeaponType.Shield:
                    return new WeaponComboConfig(2, 0.20f, 0.55f);
                case WeaponType.Spear:
                    return new WeaponComboConfig(3, 0.18f, 0.52f);
                case WeaponType.Halberd:
                    return new WeaponComboConfig(3, 0.22f, 0.58f);
                case WeaponType.Bow:
                    return new WeaponComboConfig(3, 0.10f, 0.40f);
                case WeaponType.Bowgun:
                    return new WeaponComboConfig(3, 0.08f, 0.38f);
                case WeaponType.Staff:
                    return new WeaponComboConfig(2, 0.30f, 0.65f);
                case WeaponType.MagicDevice:
                    return new WeaponComboConfig(2, 0.20f, 0.55f);
                case WeaponType.Dagger:
                    return new WeaponComboConfig(4, 0.10f, 0.40f);
                case WeaponType.Knuckle:
                    return new WeaponComboConfig(4, 0.08f, 0.38f);
                case WeaponType.Katana:
                    return new WeaponComboConfig(3, 0.18f, 0.48f);
                case WeaponType.DualWield:
                    return new WeaponComboConfig(4, 0.12f, 0.45f);
                case WeaponType.Axe:
                    return new WeaponComboConfig(3, 0.22f, 0.58f);
                default:
                    return new WeaponComboConfig(3, 0.15f, 0.50f);
            }
        }

        private struct WeaponComboConfig
        {
            public int ComboLength;
            public float StartupEnd;
            public float ActiveEnd;

            public WeaponComboConfig(int combo, float startup, float active)
            {
                ComboLength = combo;
                StartupEnd = startup;
                ActiveEnd = active;
            }
        }
    }
}
