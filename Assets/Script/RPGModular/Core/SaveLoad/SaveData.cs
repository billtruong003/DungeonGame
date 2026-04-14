using System;

namespace RPGModular
{
    [Serializable]
    public class SaveData
    {
        public string saveVersion = "1.0";
        public float playTime;
        public string lastSaveTime;

        // Player
        public int level;
        public float currentExp;
        public int unspentStatPoints;
        public int unspentSkillPoints;
        public int[] allocatedStats;

        // Resources
        public float currentHP, currentMana, currentStamina, currentChi;

        // Inventory
        public SavedItemStack[] inventorySlots;
        public int gold;

        // Equipment
        public string[] equippedItemIDs;

        // Skills
        public SavedSkillState[] learnedSkills;
        public string[] skillBarSlots;

        // Quests
        public SavedQuestState[] quests;

        // World
        public string currentZoneID;
        public float[] playerPosition;
        public float[] playerRotation;

        // Settings
        public string language = "vi";
        public float bgmVolume = 0.7f;
        public float sfxVolume = 1f;
    }

    [Serializable]
    public struct SavedItemStack
    {
        public string itemID;
        public int quantity;
    }

    [Serializable]
    public struct SavedSkillState
    {
        public string skillID;
        public int level;
    }

    [Serializable]
    public struct SavedQuestState
    {
        public string questID;
        public QuestState state;
        public int[] objectiveProgress;
    }
}
