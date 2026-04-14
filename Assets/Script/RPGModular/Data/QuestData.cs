using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Quest Data")]
    [BillTitle("Quest", "Quest definition")]
    public class QuestData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string questID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillLabelText("Desc Key (Loc)")]
        public string descKey;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public QuestType questType;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;

        [BillBoxGroup("Requirements")]
        public int requiredLevel;
        public QuestData[] prerequisiteQuests;

        [BillBoxGroup("Objectives")]
        [BillTableList]
        public QuestObjective[] objectives;

        [BillBoxGroup("Rewards")]
        public float expReward;
        public int goldReward;
        public int spReward;
        [BillTableList]
        public QuestRewardItem[] itemRewards;

        [BillBoxGroup("Repeatable")]
        public bool isRepeatable;
        [BillShowIf("isRepeatable")]
        public float repeatCooldown;
    }

    [Serializable]
    public class QuestObjective
    {
        [BillEnumToggleButtons]
        public ObjectiveType type;
        [BillLabelText("Desc Key (Loc)")]
        public string descKey;

        [BillShowIf("type", ObjectiveType.Kill)]
        public EnemyData targetEnemy;
        [BillShowIf("type", ObjectiveType.Collect)]
        public ItemData targetItem;
        public int requiredCount = 1;
    }

    [Serializable]
    public class QuestRewardItem
    {
        public ItemData item;
        public int quantity;
    }
}
