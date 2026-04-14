namespace RPGModular
{
    // ═══════════════════════════════════════════════════════
    // Resources — extends existing ResourceType in HealthSystem.cs
    // Note: ResourceType (HP, Mana, Stamina) is in HealthSystem.cs
    //       Chi is added there as 4th resource
    // ═══════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════
    // Equipment
    // ═══════════════════════════════════════════════════════

    public enum EquipSlot
    {
        Head,
        Body,
        Legs,
        Feet,
        MainHand,
        OffHand,
        Accessory1,
        Accessory2
    }

    // ═══════════════════════════════════════════════════════
    // Items
    // ═══════════════════════════════════════════════════════

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Material,
        QuestItem,
        Accessory,
        CaptureItem,
        CraftingTool,
        PetFood,
        EnhancementStone
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    // ═══════════════════════════════════════════════════════
    // Skills
    // ═══════════════════════════════════════════════════════

    public enum SkillTreeType
    {
        // Weapon (8)
        Blade,          // Kiếm Pháp — 1H Sword
        GreatSword,     // Trọng Kiếm Đạo — 2H Sword
        Katana,         // Nhẫn Đạo — Katana
        DualSword,      // Song Kiếm Thuật — Dual Wield
        Guardian,       // Thủ Thuật — Sword+Shield
        Spear,          // Thương Pháp — Spear
        Halberd,        // Kích Pháp — Halberd
        Archery,        // Xạ Thuật — Bow

        // Shared (3)
        Martial,        // Võ Thuật — Knuckle/Barehand
        Tao,            // Đạo Thuật — Chi-based
        Sorcery,        // Ma Thuật — Staff/MagicDevice

        // Life (3)
        Blacksmith,     // Luyện Khí Sư
        Alchemist,      // Điều Chế Sư
        Tamer,          // Ngự Thú Sư

        // Universal (1)
        Survival        // Sinh Tồn
    }

    public enum SkillCategory { Active, Passive }

    public enum SkillTargetType
    {
        Self,
        SingleTarget,
        AoE_Circle,
        AoE_Cone,
        AoE_Line,
        Projectile,
        Party
    }

    public enum DamageScaleType { Physical, Magical }

    // ═══════════════════════════════════════════════════════
    // Enemy AI
    // ═══════════════════════════════════════════════════════

    public enum ThreatLevel
    {
        Terrified,
        Wary,
        Normal,
        Aggressive,
        Bloodlust
    }

    public enum EnemyAIState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Attack,
        Retreat,
        Flee,
        ReactiveDefend,
        Dead
    }

    public enum EnemyTier { Normal, Elite, MiniBoss, Boss }

    // ═══════════════════════════════════════════════════════
    // Quest
    // ═══════════════════════════════════════════════════════

    public enum QuestType { Main, Side, Daily, Weekly }
    public enum QuestState { Available, Active, Completed, TurnedIn }
    public enum ObjectiveType { Kill, Collect, Talk, Reach, Craft, Capture }

    // ═══════════════════════════════════════════════════════
    // Tamer
    // ═══════════════════════════════════════════════════════

    public enum PetState { Idle, Following, Fighting, Stored }
    public enum PetRarity { Common, Uncommon, Rare, Epic, Legendary }

    // ═══════════════════════════════════════════════════════
    // Crafting
    // ═══════════════════════════════════════════════════════

    public enum CraftType { Forge, Brew, Enhance }
    public enum EnhanceResult { Success, Fail, Downgrade }

    // ═══════════════════════════════════════════════════════
    // Dialogue
    // ═══════════════════════════════════════════════════════

    public enum DialogueNodeType { Text, Choice, Condition, Event }

    // ═══════════════════════════════════════════════════════
    // NPC
    // ═══════════════════════════════════════════════════════

    public enum NPCRole { Merchant, QuestGiver, Blacksmith, Alchemist, Trainer, PetTrainer }

    // ═══════════════════════════════════════════════════════
    // Zone
    // ═══════════════════════════════════════════════════════

    public enum ZoneType { Town, Field, Dungeon, Boss, Arena }
}
