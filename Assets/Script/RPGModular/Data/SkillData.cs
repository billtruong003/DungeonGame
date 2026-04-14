using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Skill Data")]
    [BillTitle("Skill Data", "Skill definition")]
    public class SkillData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string skillID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillLabelText("Desc Key (Loc)")]
        public string descKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;
        [BillBoxGroup("Identity")]
        public SkillTreeType treeType;

        [BillTabGroup("Config", "Type"), BillEnumToggleButtons]
        public SkillCategory category;
        [BillTabGroup("Config", "Type"), BillEnumToggleButtons]
        public SkillTargetType targetType;
        [BillTabGroup("Config", "Type"), BillEnumToggleButtons]
        public DamageScaleType scaleType;
        [BillTabGroup("Config", "Type")]
        public WeaponType[] requiredWeapons;

        [BillTabGroup("Config", "Cost")]
        public int baseMPCost;
        [BillTabGroup("Config", "Cost")]
        [BillInfoBox("Chi cost for TAO skills. 0 = no Chi cost.")]
        public int baseChiCost;
        [BillTabGroup("Config", "Cost")]
        [BillSlider(0f, 5f), BillSuffix("s")]
        public float castTime;
        [BillTabGroup("Config", "Cost")]
        [BillSlider(0f, 60f), BillSuffix("s")]
        public float cooldown;

        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        public float basePower = 100f;
        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        public float powerPerLevel = 15f;
        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        public int hitCount = 1;
        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        public StatType primaryScalingStat;
        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        [BillSlider(0f, 2f)]
        public float scalingRatio = 0.5f;
        [BillTabGroup("Config", "Damage")]
        [BillShowIf("category", SkillCategory.Active)]
        public bool hasSuperArmor;

        [BillTabGroup("Config", "AoE")]
        [BillShowIf("category", SkillCategory.Active)]
        public float aoeRadius;
        [BillTabGroup("Config", "AoE")]
        [BillShowIf("category", SkillCategory.Active)]
        public float coneAngle;
        [BillTabGroup("Config", "AoE")]
        [BillShowIf("category", SkillCategory.Active)]
        public float range;

        [BillBoxGroup("Effects")]
        [BillShowIf("category", SkillCategory.Active)]
        [BillInlineEditor]
        public StatusEffectData appliedEffect;
        [BillShowIf("category", SkillCategory.Active)]
        [BillSlider(0f, 1f)]
        public float effectChance;
        [BillShowIf("category", SkillCategory.Active)]
        [BillInlineEditor]
        public StatusEffectData selfBuff;
        [BillShowIf("category", SkillCategory.Active)]
        public float buffDuration;

        [BillBoxGroup("Passive")]
        [BillShowIf("category", SkillCategory.Passive)]
        [BillTableList]
        public StatBonus[] passiveBonuses;

        [BillBoxGroup("Skill Tree")]
        [BillSlider(1, 5)]
        public int tier = 1;
        [BillTableList]
        public SkillPrerequisite[] prerequisites;
        [BillSlider(1, 10)]
        public int maxLevel = 10;
        public int[] spCostPerLevel = { 1, 1, 1, 2, 2, 2, 3, 3, 3, 3 };

        [BillBoxGroup("Animation")]
        [BillShowIf("category", SkillCategory.Active)]
        public string vatAnimClip;
        [BillShowIf("category", SkillCategory.Active)]
        public float animationDuration = 1f;
        [BillShowIf("category", SkillCategory.Active)]
        [BillSlider(0f, 2f), BillSuffix("s")]
        public float comboWindowAfter = 0.5f;
        [BillShowIf("category", SkillCategory.Active)]
        public string hitVFXId;
        [BillShowIf("category", SkillCategory.Active)]
        public string castVFXId;
        [BillShowIf("category", SkillCategory.Active)]
        public string projectilePrefabId;
        [BillShowIf("category", SkillCategory.Active)]
        public bool canBeInterrupted = true;

        [BillBoxGroup("Special Flags")]
        public bool isBlockSkill;
        public bool isParrySkill;
        [BillShowIf("isBlockSkill"), BillSlider(0f, 1f)]
        public float blockDamageReduction = 0.4f;
        [BillShowIf("isBlockSkill")]
        public float blockDuration = 1.5f;
        [BillShowIf("isBlockSkill")]
        public float blockStaminaCost = 15f;
        [BillShowIf("isParrySkill")]
        public float parryWindow = 0.3f;
        [BillShowIf("isParrySkill")]
        public float parryStaggerDuration = 1f;
    }
}
