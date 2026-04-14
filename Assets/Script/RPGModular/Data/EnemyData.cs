using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Enemy Data")]
    [BillTitle("Enemy Data", "Enemy type definition")]
    public class EnemyData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string enemyID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public EnemyTier tier;

        [BillBoxGroup("Stats")]
        [BillSlider(1, 100)] public int baseLevel = 1;
        public float baseHP = 100f;
        public float baseDamage = 10f;
        public float moveSpeed = 3.5f;
        public float physicalDefense;
        public float magicDefense;
        public DamageType damageType;

        [BillBoxGroup("Combat Behavior")]
        public float attackRange = 2f;
        public float attackCooldown = 2f;
        public float detectionRange = 12f;
        [BillSlider(0f, 1f)] public float dodgeChance;
        [BillSlider(0f, 1f)] public float blockChance;

        [BillBoxGroup("VAT Animation")]
        public string idleClip = "Idle";
        public string walkClip = "Walk";
        public string[] attackClips;
        public string hitClip = "Hit";
        public string deathClip = "Death";
        public float attackWindupTime = 0.3f;
        public float attackActiveTime = 0.2f;
        public float attackRecoveryTime = 0.5f;

        [BillBoxGroup("Rewards")]
        public float expReward = 50f;
        public int goldReward = 10;
        [BillInlineEditor]
        public LootTable lootTable;

        [BillBoxGroup("Pack Behavior")]
        public bool overridePackBehavior;
        [BillShowIf("overridePackBehavior")]
        public int preferredPackSize = 5;
        [BillShowIf("overridePackBehavior")]
        public float aggroRadius = 15f;

        [BillBoxGroup("Tamer")]
        public bool isCapturable;
        [BillShowIf("isCapturable"), BillSlider(0f, 1f)]
        public float baseCaptureRate = 0.1f;
    }
}
