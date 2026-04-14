using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Pet Data")]
    [BillTitle("Pet Data", "Pet species definition")]
    public class PetData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string petID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public PetRarity rarity;
        public GameObject vatPrefab;

        [BillBoxGroup("Base Stats")]
        public float baseHP = 50f;
        public float baseDamage = 8f;
        public float moveSpeed = 4f;
        public float attackRange = 1.5f;
        public float attackCooldown = 2f;

        [BillBoxGroup("Growth")]
        public float hpPerLevel = 10f;
        public float damagePerLevel = 2f;

        [BillBoxGroup("Skills")]
        public SkillData[] petSkills;

        [BillBoxGroup("Capture")]
        [BillSlider(0f, 1f)]
        public float baseCaptureRate = 0.1f;

        [BillBoxGroup("Fuse Bonus")]
        [BillTableList]
        public StatBonus[] fuseBonuses;
    }
}
