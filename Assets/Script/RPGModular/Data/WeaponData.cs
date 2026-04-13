using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "RPG/Weapon Data")]
    public class WeaponData : ScriptableObject, IWeapon
    {
        [BillTitle("Weapon Data")]
        [BillBoxGroup("Basic Info")]
        [SerializeField] private string weaponName = "New Weapon";
        [BillBoxGroup("Basic Info")]
        [BillEnumToggleButtons]
        [SerializeField] private WeaponType type = WeaponType.Sword;
        [BillBoxGroup("Basic Info")]
        [BillEnumToggleButtons]
        [SerializeField] private WeaponSlot slot = WeaponSlot.MainHand;
        [BillBoxGroup("Basic Info")]
        [BillPreviewField(48)]
        [SerializeField] private Sprite icon;
        [BillBoxGroup("Basic Info")]
        [BillResizableTextArea(2, 5)]
        [SerializeField] private string description;

        [BillBoxGroup("Visual")]
        [SerializeField] private GameObject weaponPrefab;

        [BillBoxGroup("Damage")]
        [BillEnumToggleButtons]
        [SerializeField] private DamageType primaryDamageType = DamageType.Slash;
        [BillBoxGroup("Damage")]
        [SerializeField] private PhysicalDamageGroup damageGroup = PhysicalDamageGroup.Slash;
        [BillBoxGroup("Damage")]
        [BillSlider(0, 500)]
        [SerializeField] private float baseDamage = 10f;
        [BillBoxGroup("Damage")]
        [BillSlider(0.5f, 10f), BillSuffix("m")]
        [SerializeField] private float attackRange = 2f;
        [BillBoxGroup("Damage")]
        [BillSlider(0.5f, 3f)]
        [SerializeField] private float attackSpeedModifier = 1.0f;

        [BillFoldoutGroup("Stat Requirements")]
        [SerializeField] private StatRequirement[] requirements;

        [BillFoldoutGroup("Stat Bonuses")]
        [SerializeField] private StatBonus[] statBonuses;

        [BillFoldoutGroup("Animation")]
        [SerializeField] private bool useDefaultAnimSet = true;
        [BillFoldoutGroup("Animation")]
        [BillHideIf("useDefaultAnimSet")]
        [SerializeField] private WeaponAnimationSet customAnimationSet;

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

        public Sprite Icon => icon;
        public string Description => description;
        public GameObject WeaponPrefab => weaponPrefab;
        public StatRequirement[] Requirements => requirements;
        public StatBonus[] StatBonuses => statBonuses;

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
