// File: Data/WeaponData.cs
// ScriptableObject chứa data cho mỗi vũ khí
// Tạo trong Unity Editor: Create → RPG → Weapon Data
using UnityEngine;

namespace RPGModular
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "RPG/Weapon Data")]
    public class WeaponData : ScriptableObject, IWeapon
    {
        [Header("Basic Info")]
        [SerializeField] private string weaponName = "New Weapon";
        [SerializeField] private WeaponType type = WeaponType.Sword;
        [SerializeField] private WeaponSlot slot = WeaponSlot.MainHand;
        [SerializeField] private Sprite icon;
        [SerializeField] [TextArea] private string description;

        [Header("Damage")]
        [SerializeField] private DamageType primaryDamageType = DamageType.Slash;
        [SerializeField] private PhysicalDamageGroup damageGroup = PhysicalDamageGroup.Slash;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackSpeedModifier = 1.0f;

        [Header("Stat Requirements")]
        [SerializeField] private StatRequirement[] requirements;

        [Header("Stat Bonuses (khi equip)")]
        [SerializeField] private StatBonus[] statBonuses;

        [Header("Animation")]
        [SerializeField] private WeaponAnimationSet customAnimationSet;
        [SerializeField] private bool useDefaultAnimSet = true;

        // IWeapon implementation
        public string WeaponName => weaponName;
        public WeaponType Type => type;
        public WeaponSlot Slot => slot;
        public DamageType PrimaryDamageType => primaryDamageType;
        public PhysicalDamageGroup DamageGroup => damageGroup;
        public float BaseDamage => baseDamage;
        public float AttackRange => attackRange;
        public float AttackSpeedModifier => attackSpeedModifier;

        public WeaponAnimationSet AnimationSet
        {
            get
            {
                if (useDefaultAnimSet || customAnimationSet == null)
                    return WeaponAnimationSet.CreateDefault(type);
                return customAnimationSet;
            }
        }

        // Extra data
        public Sprite Icon => icon;
        public string Description => description;
        public StatRequirement[] Requirements => requirements;
        public StatBonus[] StatBonuses => statBonuses;

        /// <summary>
        /// Kiểm tra nhân vật có đủ stat để equip không
        /// </summary>
        public bool CanEquip(IStatProvider stats)
        {
            if (requirements == null) return true;
            foreach (var req in requirements)
            {
                if (stats.GetStat(req.Stat) < req.MinValue)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Tạo danh sách StatModifier từ bonus (để AddModifier khi equip)
        /// </summary>
        public StatModifier[] CreateEquipModifiers()
        {
            if (statBonuses == null) return new StatModifier[0];
            
            var mods = new StatModifier[statBonuses.Length];
            for (int i = 0; i < statBonuses.Length; i++)
            {
                var bonus = statBonuses[i];
                mods[i] = new StatModifier(bonus.Stat, bonus.Type, bonus.Value, 0, this);
            }
            return mods;
        }
    }

    [System.Serializable]
    public class StatRequirement
    {
        public StatType Stat;
        public float MinValue;
    }

    [System.Serializable]
    public class StatBonus
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }
}
