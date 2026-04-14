# Game Architecture Document — Complete Resolution
### Version 2.0 | All Systems Resolved | SpacetimeDB-Ready

---

## 0. RESOLVED DECISIONS

### D1: BillGameCore = Self-Contained Framework

BillGameCore là framework **chung cho mọi game**. RPGModular **ngồi trên** BillGameCore, gọi services qua `Bill.*`.

```
┌─────────────────────────────────────────────┐
│  RPGModular (Game-specific logic)           │
│  Game.Stats, Game.Inv, Game.Skill...        │
├─────────────────────────────────────────────┤
│  BillGameCore v3 (Generic framework)        │
│  Bill.Pool, Bill.Audio, Bill.Save,          │
│  Bill.Events, Bill.UI, Bill.Scene...        │
├─────────────────────────────────────────────┤
│  Unity Engine                               │
└─────────────────────────────────────────────┘
```

**Quy tắc**: RPGModular KHÔNG duplicate gì BillGameCore đã có. Cần pool → dùng `Bill.Pool`. Cần audio → dùng `Bill.Audio`. Cần UI → dùng `Bill.UI`.

### D2: Block/Parry = Default Skills

Bỏ BlockingState, ParrySuccessState, GuardBreakState khỏi CombatSM.
Block/Parry trở thành **2 SkillData mặc định** (pre-learned level 1, 0 SP cost).
Chạy qua SkillExecuteState giống mọi skill khác.
Shield tree (Thủ Thuật) nâng cấp chúng mạnh hơn.

### D3: Enemy Animation = VAT Only

Tất cả enemy (mob + boss) dùng VAT. Không có Skinned variant.
Boss phức tạp hơn (nhiều clip, phase) nhưng vẫn VAT.
Target picking = OverlapSphere theo position, không lock-on cho AoE.

### D4: TakeDamage Signature

```
Enemy:  TakeDamage(float amount)           ← đơn giản, VAT enemy không cần DamageInfo
Player: TakeDamage(DamageInfo info)         ← phức tạp, qua DamagePipeline
```

### D5: Event Strategy — Hybrid

```
C# events (Action<T>)    → Component-level, cùng GameObject hoặc direct reference
                            Ví dụ: HealthSystem.OnDeath, Inventory.OnSlotChanged
Bill.Events (IEventBus)   → Cross-system, loose coupling, không cần reference
                            Ví dụ: OnPlayerLevelUp → UI + Audio + VFX all respond
```

### D6: Loc.Get() vs Game.Loc

`Loc.Get("key")` = primary shortcut (static class, 1 dòng).
`Game.Loc` = khi cần access service object (SetLanguage, AvailableLanguages).

### D7: WeaponTypes Without Trees

15 weapon types tồn tại. 8 có skill tree. 7 còn lại (Bowgun, Axe, etc.) = usable weapons nhưng dùng skill tree khác (ví dụ: Axe dùng Trọng Kiếm tree, Bowgun dùng Xạ Thuật tree). Future content có thể thêm tree.

### D8: SpacetimeDB-Ready Design

Mọi state mutation qua Game.* facade → dễ wrap network layer sau.
Mọi system có events → dễ chuyển sang subscription model.
Logic tách: client prediction (movement, animation) vs server authority (damage, loot, economy).

---

## 1. BILLGAMECORE INTEGRATION MAP

### Dùng Bill.* ở đâu

```
Bill.Pool       → Enemy spawn/despawn, Projectile, VFX particles, DamagePopup, LootDrop
Bill.Audio      → Hit SFX, Skill SFX, BGM per zone, UI SFX, Ambient
Bill.Save       → Game state persistence (inventory, equipment, skills, quests, level, position)
Bill.UI         → ALL panels: HUD, Inventory, Equipment, SkillTree, Quest, Dialogue, Shop, Craft, Pet, Settings
Bill.Scene      → Zone loading, Town→Field transition, fade in/out
Bill.Events     → Cross-system broadcasts: OnPlayerLevelUp, OnZoneEnter, OnBossDefeated, OnQuestComplete
Bill.Timer      → Optional: cooldown timers, buff durations (coroutine cũng OK)
Bill.Config     → Future: balance tuning from external config
```

### Dùng C# events ở đâu

```
HealthSystem.OnDeath              → CombatSM, PlayerController (direct reference)
Inventory.OnSlotChanged           → InventoryPanel (direct reference)
SkillCaster.OnSkillCastComplete   → ComboTracker (direct reference)
EnemyBase.OnDeath                 → PackManager, LootSystem (direct reference)
```

### EnemyBase inherits PooledObject

```csharp
public abstract class EnemyBase : PooledObject, IDamageable
{
    public override void OnSpawnedFromPool()
    {
        ResetHP();
        ResetAI();
    }

    public override void OnReturnedToPool()
    {
        CleanupEffects();
    }
}
```

### Spawn pattern

```csharp
// VAT_MobSpawner dùng Bill.Pool:
GameObject enemy = Bill.Pool.Spawn(vatEnemyPrefab, spawnPos, spawnRot);
enemy.GetComponent<EnemyBase>().Initialize(enemyLevel, hpMult, dmgMult);

// Despawn:
enemy.GetComponent<EnemyBase>().ReturnToPool();
// hoặc: Bill.Pool.Return(enemy);
```

---

## 2. COMPLETE ENUM DEFINITIONS

```csharp
// ===== File: Enums/GameEnums.cs =====

// --- Resources ---
public enum ResourceType { HP, Mana, Stamina, Chi }

// --- Stats ---
// StatType đã có trong IStatProvider.cs:
// STR, INT, AGI, DEX, VIT, LUK, TECH

// --- Equipment ---
public enum EquipSlot
{
    Head, Body, Legs, Feet,
    MainHand, OffHand,
    Accessory1, Accessory2
}

// --- Items ---
public enum ItemType
{
    Weapon, Armor, Consumable, Material,
    QuestItem, Accessory, CaptureItem, CraftingTool,
    PetFood, EnhancementStone
}

public enum ItemRarity
{
    Common,      // white
    Uncommon,    // green
    Rare,        // blue
    Epic,        // purple
    Legendary    // gold
}

// --- Skills ---
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
    Blacksmith,     // Luyện Khí Sư — Craft weapons/armor
    Alchemist,      // Điều Chế Sư — Potions/bombs
    Tamer,          // Ngự Thú Sư — Pet capture/raise

    // Universal (1)
    Survival        // Sinh Tồn — Passive HP/resist/dodge
}

public enum SkillCategory { Active, Passive }

public enum SkillTargetType
{
    Self,           // buff/heal
    SingleTarget,   // lock-on target
    AoE_Circle,     // OverlapSphere quanh caster
    AoE_Cone,       // cone phía trước
    AoE_Line,       // line phía trước (spear thrust)
    Projectile,     // bắn ra đạn
    Party           // buff cả party (future MMORPG)
}

public enum DamageScaleType { Physical, Magical }

// --- Combat ---
// ECombatState — UPDATED (bỏ Block/Parry/GuardBreak, thêm Skill states)
public enum ECombatState
{
    Idle, Combat, Attacking,
    SkillCharge, SkillExecute, ComboReady,
    Dodge, HitStun, Knockback, Dead
}

// --- Enemy AI ---
public enum ThreatLevel
{
    Terrified,      // gap >= +10, flee
    Wary,           // gap +5→+9, reactive only
    Normal,         // gap -2→+4, 2-3 chasers
    Aggressive,     // gap -3→-7, 4-5 chasers
    Bloodlust       // gap <= -8, all chase
}

public enum EnemyAIState
{
    Idle, Patrol, Alert, Chase, Attack,
    Retreat, Flee, ReactiveDefend, Dead
}

public enum EnemyTier { Normal, Elite, MiniBoss, Boss }

// --- Quest ---
public enum QuestType { Main, Side, Daily, Weekly }
public enum QuestState { Available, Active, Completed, TurnedIn }
public enum ObjectiveType { Kill, Collect, Talk, Reach, Craft, Capture }

// --- Tamer ---
public enum PetState { Idle, Following, Fighting, Stored }
public enum PetRarity { Common, Uncommon, Rare, Epic, Legendary }

// --- Crafting ---
public enum CraftType { Forge, Brew, Enhance }
public enum EnhanceResult { Success, Fail, Downgrade }

// --- Dialogue ---
public enum DialogueNodeType { Text, Choice, Condition, Event }

// --- Zone ---
public enum ZoneType { Town, Field, Dungeon, Boss, Arena }
```

---

## 3. SHARED DATA CLASSES

```csharp
// ===== File: Data/SharedDataTypes.cs =====

[Serializable]
public class StatBonus
{
    public StatType stat;
    public ModifierType modType;    // Flat, PercentAdd, PercentMult
    public float value;
}

[Serializable]
public class StatRequirement
{
    public StatType stat;
    public int requiredValue;
}

[Serializable]
public struct ItemStack
{
    public ItemData Data;
    public int Quantity;
    public bool IsEmpty => Data == null || Quantity <= 0;

    public static ItemStack Empty => new ItemStack { Data = null, Quantity = 0 };
}

[Serializable]
public class SkillPrerequisite
{
    public SkillData skill;
    public int requiredLevel;
}

[Serializable]
public class LootEntry
{
    public ItemData item;
    public int minQuantity = 1;
    public int maxQuantity = 1;
    [BillSlider(0f, 1f)] public float dropChance = 0.1f;
}

[Serializable]
public class ActiveStatusEffect
{
    public StatusEffectData Data;
    public float RemainingDuration;
    public int CurrentStacks;
    public float TickTimer;
    public object Source;   // who applied it

    // SpacetimeDB-ready: serializable state
    public string EffectId => Data.effectID;
}
```

---

## 4. SCRIPTABLEOBJECT DEFINITIONS — Complete

### 4.1 StatusEffectData (CHƯA define trước đây)

```csharp
[CreateAssetMenu(menuName = "Game/Status Effect Data")]
[BillTitle("Status Effect", "Buff/Debuff definition")]
public class StatusEffectData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string effectID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillLabelText("Desc Key (Loc)")] public string descKey;
    [BillPreviewField] public Sprite icon;

    [BillBoxGroup("Type")]
    public bool isDebuff;
    public bool isPermanent;    // buff lasts until removed (passive, equip)

    [BillBoxGroup("Duration")]
    [BillShowIf("@!isPermanent")]
    [BillSlider(0f, 300f), BillSuffix("s")]
    public float baseDuration = 10f;

    [BillBoxGroup("Stacking")]
    public StackBehavior stackBehavior;     // Refresh, AddDuration, StackIntensity, StackSeparate
    [BillShowIf("stackBehavior", StackBehavior.StackIntensity)]
    public int maxStacks = 5;

    [BillBoxGroup("Tick Effect")]
    [BillInfoBox("Damage/heal per tick. Negative = damage (DoT), positive = heal (HoT)")]
    public float tickValue;
    [BillSlider(0.5f, 5f), BillSuffix("s")]
    public float tickInterval = 1f;
    public DamageType tickDamageType;

    [BillBoxGroup("Stat Modifiers")]
    [BillTableList]
    public StatBonus[] statModifiers;       // applied while active

    [BillBoxGroup("Movement")]
    [BillSlider(0f, 2f)]
    public float moveSpeedMultiplier = 1f;  // 0.5 = slow, 1 = normal, 1.5 = haste

    [BillBoxGroup("Visual")]
    public string vfxPrefabId;              // pooled VFX on character
    public Color tintColor = Color.white;   // character tint while active
}

public enum StackBehavior { Refresh, AddDuration, StackIntensity, StackSeparate }
```

### 4.2 ArmorData (CHƯA define trước đây)

```csharp
[CreateAssetMenu(menuName = "Game/Armor Data")]
[BillTitle("Armor Data", "Trang bị phòng thủ")]
public class ArmorData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string itemID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillLabelText("Desc Key (Loc)")] public string descKey;
    [BillPreviewField] public Sprite icon;
    public ItemRarity rarity;

    [BillBoxGroup("Slot")]
    [BillEnumToggleButtons]
    public EquipSlot slot;  // Head, Body, Legs, Feet

    [BillBoxGroup("Defense")]
    public float physicalDefense;
    public float magicDefense;

    [BillBoxGroup("Stat Bonuses")]
    [BillTableList]
    public StatBonus[] equipBonuses;

    [BillBoxGroup("Requirements")]
    [BillTableList]
    public StatRequirement[] requirements;

    [BillBoxGroup("Economy")]
    public int sellPrice;

    [BillBoxGroup("Visual")]
    public GameObject meshPrefab;   // visual mesh swap (future)
}
```

### 4.3 SkillTreeData (CHƯA define trước đây)

```csharp
[CreateAssetMenu(menuName = "Game/Skill Tree Data")]
[BillTitle("Skill Tree", "1 nhánh võ học")]
public class SkillTreeData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public SkillTreeType treeType;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillLabelText("Desc Key (Loc)")] public string descKey;
    [BillPreviewField] public Sprite icon;

    [BillBoxGroup("Requirements")]
    public WeaponType[] compatibleWeapons;  // empty = any

    [BillBoxGroup("Skills")]
    [BillTableList]
    public SkillData[] skills;              // all skills in this tree, ordered by tier

    [BillBoxGroup("Tier Unlock")]
    [BillInfoBox("Số skill đã học ở tier N để unlock tier N+1")]
    public int[] tierUnlockRequirements;    // index = tier, value = skills needed
    // ví dụ: {0, 0, 2, 3, 4} = tier 3 cần 2 skills, tier 4 cần 3, tier 5 cần 4
}
```

### 4.4 LocalizationConfig (CHƯA define trước đây)

```csharp
[CreateAssetMenu(menuName = "Game/Localization Config")]
[BillTitle("Localization Config", "Cấu hình đa ngôn ngữ")]
public class LocalizationConfig : ScriptableObject
{
    [BillTableList]
    public LanguageEntry[] supportedLanguages;

    public string defaultLanguage = "vi";
    public string fallbackLanguage = "en";
}

[Serializable]
public class LanguageEntry
{
    public string code;             // "vi", "en", "ja"
    public string displayName;      // "Tiếng Việt", "English"
    public TMP_FontAsset font;      // font cho ngôn ngữ đó (CJK, Thai cần font riêng)
    public bool isRTL;              // right-to-left (Arabic, Hebrew — future)
}
```

### 4.5 EnemyData — Updated cho Pack system

```csharp
[CreateAssetMenu(menuName = "Game/Enemy Data")]
[BillTitle("Enemy Data", "Định nghĩa 1 loại enemy")]
public class EnemyData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string enemyID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillPreviewField] public Sprite icon;
    [BillEnumToggleButtons] public EnemyTier tier;

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
    public string[] attackClips;            // "Attack1", "Attack2"
    public string hitClip = "Hit";
    public string deathClip = "Death";
    public float attackWindupTime = 0.3f;
    public float attackActiveTime = 0.2f;
    public float attackRecoveryTime = 0.5f;

    [BillBoxGroup("Rewards")]
    public float expReward = 50f;
    public int goldReward = 10;
    [BillInlineEditor] public LootTable lootTable;

    [BillBoxGroup("Pack Behavior")]
    [BillInfoBox("Override PackManager defaults cho enemy type này")]
    public bool overridePackBehavior;
    [BillShowIf("overridePackBehavior")]
    public int preferredPackSize = 5;
    [BillShowIf("overridePackBehavior")]
    public float aggroRadius = 15f;

    [BillBoxGroup("Tamer")]
    [BillInfoBox("Có thể bắt bằng Ngự Thú Sư?")]
    public bool isCapturable;
    [BillShowIf("isCapturable")]
    [BillSlider(0f, 1f)] public float baseCaptureRate = 0.1f;
    [BillShowIf("isCapturable")]
    public PetData petDataWhenCaptured;     // ref to pet version of this enemy
}
```

### 4.6 LootTable SO

```csharp
[CreateAssetMenu(menuName = "Game/Loot Table")]
[BillTitle("Loot Table", "Bảng drop rate")]
public class LootTable : ScriptableObject
{
    [BillTableList]
    public LootEntry[] entries;

    [BillBoxGroup("Guaranteed")]
    public ItemData guaranteedDrop;         // always drop (null = none)
    public int guaranteedMinQty = 1;
    public int guaranteedMaxQty = 1;

    /// Roll all entries, return list of (item, quantity) drops
    public List<(ItemData item, int qty)> Roll()
    {
        var result = new List<(ItemData, int)>();
        if (guaranteedDrop != null)
            result.Add((guaranteedDrop, Random.Range(guaranteedMinQty, guaranteedMaxQty + 1)));

        foreach (var e in entries)
        {
            if (Random.value <= e.dropChance)
                result.Add((e.item, Random.Range(e.minQuantity, e.maxQuantity + 1)));
        }
        return result;
    }
}
```

---

## 5. BLOCK/PARRY — DEFAULT SKILL DESIGN

### Concept

Block và Parry là **2 skill mặc định** mà mọi nhân vật có sẵn (level 1, 0 SP).
Chúng yếu hơn skill tree. Shield tree (Thủ Thuật) nâng cấp chúng.

### Default Block Skill

```csharp
// SkillData SO: "default_block"
skillID = "default_block"
nameKey = "skill.default.block.name"         // "Phòng Thủ"
category = Active
targetType = Self
requiredWeapons = []                          // any weapon, kể cả tay không
baseMPCost = 0                                // không tốn MP
baseChiCost = 0
castTime = 0                                  // instant
cooldown = 0.5f                               // chống spam

// Behavior (custom in SkillExecuteState):
// → Player enters block stance for 1.5s
// → If hit during stance: damage reduced 40%, consume stamina
// → If stamina depleted: guard break → HitStun 1s
// → Heavy attack: damage reduced 20% only
// → Animation: generic block pose (works all weapons)

// Thủ Thuật tree upgrades:
// Level 5 block: 60% reduction, less stamina cost
// Level 10 block: 80% reduction, reflect 10% damage
```

### Default Parry Skill

```csharp
// SkillData SO: "default_parry"
skillID = "default_parry"
nameKey = "skill.default.parry.name"         // "Đỡ Đòn"
category = Active
targetType = Self
requiredWeapons = []
baseMPCost = 0
baseChiCost = 0
castTime = 0
cooldown = 3f                                 // longer cooldown = high risk

// Behavior:
// → Player enters parry window for 0.3s (tight timing)
// → If hit during window: negate ALL damage, enemy staggered 1s
// → If NOT hit: 0.5s recovery (punishable)
// → Animation: quick deflect motion

// Thủ Thuật tree upgrades:
// Level 5 parry: window 0.4s, riposte attack after success
// Nhẫn Đạo (Katana) tree: Counter stance = parry but costs Focus
```

### Implementation

Cả 2 chạy qua `SkillExecuteState` giống mọi skill.
Trong `SkillExecuteState.Enter()`:
- Nếu skill có tag `isBlockSkill` → enter block sub-state (giảm damage incoming)
- Nếu skill có tag `isParrySkill` → enter parry window (negate damage if timed)

```csharp
// Thêm vào SkillData:
[BillBoxGroup("Special Flags")]
public bool isBlockSkill;       // default_block, shield skills
public bool isParrySkill;       // default_parry, counter skills
[BillShowIf("isBlockSkill")]
[BillSlider(0f, 1f)]
public float blockDamageReduction = 0.4f;
[BillShowIf("isBlockSkill")]
public float blockDuration = 1.5f;
[BillShowIf("isBlockSkill")]
public float blockStaminaCost = 15f;
[BillShowIf("isParrySkill")]
public float parryWindow = 0.3f;
[BillShowIf("isParrySkill")]
public float parryStaggerDuration = 1f;
```

### SkillBar Auto-Equip

```
SkillBar: 4 slots + 2 hidden default slots
Slot 0-3: Player-assigned skills
Slot 4:   Block (default, always available, mapped to specific key)
Slot 5:   Parry (default, always available, mapped to specific key)

Input: Block = hold right-click, Parry = tap right-click (hold duration threshold 0.15s)
```

---

## 6. VAT ENEMY ANIMATION SYSTEM

### VAT_Animator — Required API

```csharp
/// <summary>
/// GPU vertex animation playback. Đã tồn tại trong VAT package.
/// API mà RPGModular cần:
/// </summary>
public class VAT_Animator : MonoBehaviour
{
    // Play clip ngay lập tức
    public void Play(string clipName);

    // Crossfade sang clip mới
    public void CrossFade(string clipName, float duration = 0.15f);

    // Query
    public string CurrentClip { get; }
    public float NormalizedTime { get; }        // 0-1
    public float ClipDuration(string clipName); // seconds

    // Speed control
    public float Speed { get; set; }            // 1 = normal
}
```

### Phase Tracking — Manual Timer

VAT không có Animator events. EnemyBase tự track phase bằng timer:

```csharp
// Trong EnemyBase:
private float _attackTimer;
private AttackPhase _attackPhase;

void UpdateAttackPhase()
{
    _attackTimer += Time.deltaTime;
    if (_attackTimer < enemyData.attackWindupTime)
        _attackPhase = AttackPhase.Windup;      // animation đang ra chiêu
    else if (_attackTimer < enemyData.attackWindupTime + enemyData.attackActiveTime)
    {
        if (_attackPhase != AttackPhase.Active)
        {
            _attackPhase = AttackPhase.Active;
            PerformDamageCheck();               // GÂY DAMAGE ở đây
        }
    }
    else if (_attackTimer < totalAttackDuration)
        _attackPhase = AttackPhase.Recovery;     // hồi chiêu
    else
        ReturnToIdle();
}

enum AttackPhase { None, Windup, Active, Recovery }
```

### Target Picking — Position-based

```csharp
// Trong PerformDamageCheck():
// SingleTarget (melee):
Collider[] hits = Physics.OverlapSphere(transform.position + forward * attackRange/2,
                                         attackRange/2, playerLayer);
foreach (var hit in hits)
{
    if (hit.TryGetComponent<IDamageable>(out var target))
        target.TakeDamage(CalculateDamage());
}

// AoE (boss sweep):
// Dùng OverlapSphere lớn hơn + angle filter giống skill AoE_Cone
```

### VAT_MobSpawner — Pack Spawning

```csharp
[BillTitle("Mob Spawner", "Spawn enemy packs using VAT")]
public class VAT_MobSpawner : MonoBehaviour
{
    [BillBoxGroup("Spawn Config")]
    [BillRequired] public GameObject vatEnemyPrefab;
    [BillRequired] public EnemyData enemyData;
    [BillSlider(1, 20)] public int packSize = 5;
    [BillSlider(5f, 30f)] public float spawnRadius = 10f;

    [BillBoxGroup("Activation")]
    [BillSlider(20f, 100f)] public float activationRange = 50f;
    [BillSlider(50f, 150f)] public float despawnRange = 80f;
    [BillSlider(10f, 120f)] public float respawnDelay = 30f;

    [BillBoxGroup("Pack Manager")]
    [BillReadOnly, BillShowInInspector]
    public PackManager PackManager { get; private set; }

    // --- Lifecycle ---
    // Player enter activationRange → Bill.Pool.Spawn(prefab) × packSize
    // Scatter positions: Random.insideUnitSphere * spawnRadius
    // Each spawned enemy → PackManager.RegisterEnemy()
    // Player leave despawnRange → Bill.Pool.Return(all enemies)
    // All dead → wait respawnDelay → re-spawn
    //
    // Distance-based LOD:
    //   < activationRange: full AI + render
    //   activationRange ~ despawnRange: render idle anim only, AI paused
    //   > despawnRange: return to pool
}
```

---

## 7. CRAFTING SYSTEM (Luyện Khí Sư + Điều Chế Sư)

### 7.1 Public API

```csharp
Game.Craft.CanCraft(recipeData)              → bool (has materials? skill level?)
Game.Craft.Craft(recipeData)                 → CraftResult (Success/Fail + output item)
Game.Craft.GetAvailableRecipes(CraftType)    → List<RecipeData>
Game.Craft.Enhance(weapon, material)         → EnhanceResult (Success/Fail/Downgrade)
Game.Craft.GetEnhanceSuccessRate(weapon)     → float (0-1)
```

### 7.2 Events

```
OnCraftSuccess(RecipeData recipe, ItemData output)     → UI popup, SFX
OnCraftFail(RecipeData recipe)                          → UI "Thất bại!", SFX
OnEnhanceSuccess(WeaponData weapon, int newLevel)       → UI + VFX glow
OnEnhanceFail(WeaponData weapon, EnhanceResult)         → UI warning
```

### 7.3 RecipeData SO

```csharp
[CreateAssetMenu(menuName = "Game/Recipe Data")]
[BillTitle("Recipe", "Công thức chế tạo")]
public class RecipeData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string recipeID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillPreviewField] public Sprite icon;
    [BillEnumToggleButtons] public CraftType craftType; // Forge, Brew, Enhance

    [BillBoxGroup("Requirements")]
    public SkillTreeType requiredTree;      // Blacksmith or Alchemist
    public int requiredSkillLevel;          // skill level in that tree
    [BillTableList]
    public CraftIngredient[] ingredients;
    public int goldCost;

    [BillBoxGroup("Output")]
    [BillRequired] public ItemData outputItem;
    public int outputQuantity = 1;

    [BillBoxGroup("Success Rate")]
    [BillSlider(0f, 1f)] public float baseSuccessRate = 1f;
    // Actual rate = baseRate + (skillLevel - requiredLevel) * 0.05
    // Capped at 1.0
}

[Serializable]
public class CraftIngredient
{
    public ItemData item;
    public int quantity;
}
```

### 7.4 Weapon Enhancement Flow

```
1. Player brings weapon + Enhancement Stone + gold to Blacksmith NPC/station
2. Game.Craft.GetEnhanceSuccessRate(weapon)
   → base 100% for +1→+3
   → 80% for +4→+5
   → 60% for +6→+7
   → 40% for +8
   → 20% for +9
   → 10% for +10
   → Blacksmith skill level adds +2% per level
3. Roll → Success: weapon.enhanceLevel++ → all stats scale * (1 + 0.05 * level)
         → Fail:  +7 trở lên → downgrade 1 level (trừ khi dùng Protection Stone)
                   +6 trở xuống → không mất gì, chỉ mất material
4. Visual: enhanced weapon có glow effect (+1→+3 faint, +4→+6 medium, +7→+10 strong)
5. Enhanced level hiện trong tên: "Kiếm Sắt +7"
```

### 7.5 Alchemist Specifics

```
Potions:     HP Potion, MP Potion, Chi Potion, Stamina Potion, Antidote
Elixirs:     ATK Boost (30s), DEF Boost (30s), Speed Boost (30s), EXP Boost (10min)
Bombs:       Fire Bomb (AoE damage), Ice Bomb (AoE slow), Flash Bomb (AoE stun)
Element Oil: Fire Oil (add fire element to weapon attacks for 60s — future element system)
```

---

## 8. TAMER SYSTEM (Ngự Thú Sư)

### 8.1 Public API

```csharp
Game.Pet.ActivePet                            → PetInstance (null = no pet out)
Game.Pet.Summon(petIndex)                     → bool
Game.Pet.Recall()                             → void (pet return to storage)
Game.Pet.GetStoredPets()                      → List<PetInstance>
Game.Pet.TryCapture(enemy)                    → bool (roll capture chance)
Game.Pet.Feed(petIndex, foodItem)             → void (increase bond)
Game.Pet.Fuse()                               → void (ultimate: gain pet stats 30s)
Game.Pet.Release(petIndex)                    → PetInstance (remove from storage)
Game.Pet.StorageCapacity                      → int (default 10, upgradeable)
```

### 8.2 Events

```
OnPetCaptured(PetInstance pet)               → UI "Đã bắt được!", celebration
OnCaptureFailed(EnemyBase enemy)              → UI "Thất bại..."
OnPetSummoned(PetInstance pet)               → pet appears beside player
OnPetRecalled(PetInstance pet)               → pet disappears
OnPetLevelUp(PetInstance pet, int newLevel)  → UI notification
OnPetFuseStart(PetInstance pet)              → VFX fusion aura
OnPetFuseEnd(PetInstance pet)                → VFX end
OnBondChanged(PetInstance pet, int newBond)   → UI bond meter
```

### 8.3 PetData SO (species definition)

```csharp
[CreateAssetMenu(menuName = "Game/Pet Data")]
[BillTitle("Pet Data", "Species definition")]
public class PetData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string petID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillPreviewField] public Sprite icon;
    public PetRarity rarity;
    public GameObject vatPrefab;            // VAT model

    [BillBoxGroup("Base Stats")]
    public float baseHP = 50f;
    public float baseDamage = 8f;
    public float moveSpeed = 4f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;

    [BillBoxGroup("Growth per Level")]
    public float hpPerLevel = 10f;
    public float damagePerLevel = 2f;

    [BillBoxGroup("Skills")]
    public SkillData[] petSkills;           // max 2 skills pet có thể dùng

    [BillBoxGroup("Capture")]
    [BillSlider(0f, 1f)] public float baseCaptureRate = 0.1f;
    [BillInfoBox("Capture rate = base * (1 - enemy.currentHP/maxHP) * tamerSkillBonus")]

    [BillBoxGroup("Fuse Bonus")]
    [BillInfoBox("Stats player nhận được khi Fuse 30s")]
    [BillTableList]
    public StatBonus[] fuseBonuses;
}
```

### 8.4 PetInstance (runtime state)

```csharp
[Serializable]
public class PetInstance
{
    public PetData Data;
    public string nickname;                 // player-given name
    public int level = 1;
    public float currentExp;
    public int bond;                        // 0-100, tăng khi feed/fight together
    public PetState state = PetState.Stored;

    // Computed
    public float MaxHP => Data.baseHP + Data.hpPerLevel * (level - 1);
    public float Damage => Data.baseDamage + Data.damagePerLevel * (level - 1);
    public float CurrentHP { get; set; }

    // SpacetimeDB-ready: toàn bộ serializable
}
```

### 8.5 Capture Flow

```
1. Enemy HP < 30%
2. Player dùng CaptureItem (consumable, craftable by Alchemist)
3. Roll: captureRate = petData.baseCaptureRate * (1 - enemy.currentHP/maxHP) * tamerSkillBonus
   tamerSkillBonus = 1 + (tamerSkillLevel * 0.05)
4. Success → enemy despawn, PetInstance created, added to Pet Storage
5. Fail → CaptureItem consumed, enemy still alive
6. Boss/Elite = NOT capturable (isCapturable = false)
```

### 8.6 Pet Combat AI

```
Pet follows player. When player locked-on to enemy:
  → Pet auto-attacks same target
  → Pet uses skill khi có (cooldown-based, AI decision)
  → Pet takes damage from enemy AoE (can die → auto-recall, revive after 60s)
  → Pet gains EXP from kills (50% of player EXP)
  → Bond +1 per kill together
```

---

## 9. NPC & SHOP SYSTEM

### 9.1 Public API

```csharp
Game.Shop.OpenShop(shopData)                  → void (show ShopPanel via Bill.UI)
Game.Shop.Buy(shopData, itemIndex, quantity)   → BuyResult (Success/NoGold/NoSpace)
Game.Shop.Sell(itemData, quantity)              → int goldEarned
Game.Shop.GetSellPrice(itemData)               → int
Game.NPC.Interact(npcData)                     → void (start dialogue/shop/quest)
```

### 9.2 Events

```
OnItemBought(ItemData, int quantity, int totalCost)    → UI notification
OnItemSold(ItemData, int quantity, int goldEarned)     → UI notification
OnShopOpened(ShopData)                                 → Bill.UI.Show<ShopPanel>
OnShopClosed                                           → Bill.UI.Hide<ShopPanel>
```

### 9.3 NPCData SO

```csharp
[CreateAssetMenu(menuName = "Game/NPC Data")]
[BillTitle("NPC Data", "Non-player character")]
public class NPCData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string npcID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillPreviewField] public Sprite portrait;

    [BillBoxGroup("Interaction")]
    public NPCRole role;                    // Merchant, QuestGiver, Blacksmith, Alchemist, Trainer
    [BillShowIf("role", NPCRole.Merchant)]
    [BillInlineEditor] public ShopData shopData;
    [BillShowIf("@role == NPCRole.QuestGiver")]
    public QuestData[] availableQuests;

    [BillBoxGroup("Dialogue")]
    [BillInlineEditor] public DialogueData greetingDialogue;
}

public enum NPCRole { Merchant, QuestGiver, Blacksmith, Alchemist, Trainer, PetTrainer }
```

### 9.4 ShopData SO

```csharp
[CreateAssetMenu(menuName = "Game/Shop Data")]
[BillTitle("Shop Data", "Danh sách hàng hóa")]
public class ShopData : ScriptableObject
{
    [BillBoxGroup("Config")]
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillSlider(0.5f, 3f)] public float buyPriceMultiplier = 1f;
    [BillSlider(0.1f, 1f)] public float sellPriceMultiplier = 0.3f;

    [BillBoxGroup("Inventory")]
    [BillTableList]
    public ShopItem[] items;

    [BillBoxGroup("Restock")]
    public bool restocks = true;
    [BillShowIf("restocks")]
    public float restockInterval = 3600f;   // seconds (1 hour real time)
}

[Serializable]
public class ShopItem
{
    public ItemData item;
    public int price;                       // override price (0 = use item.sellPrice * buyMult)
    public int stock = -1;                  // -1 = infinite
}
```

---

## 10. QUEST SYSTEM

### 10.1 Public API

```csharp
Game.Quest.AcceptQuest(questData)                → bool
Game.Quest.AbandonQuest(questData)               → void
Game.Quest.GetActiveQuests()                     → List<QuestInstance>
Game.Quest.GetQuestState(questData)              → QuestState
Game.Quest.TurnIn(questData)                     → bool (claim rewards)
Game.Quest.IsObjectiveComplete(quest, index)     → bool

// Objective progress — AUTO-TRACKED:
// Kill quests: listen to EnemyBase.OnDeath → match enemy type
// Collect quests: listen to Inventory.OnItemAdded → match item type
// Talk quests: NPC.Interact() → mark complete
// Reach quests: trigger zone enter → mark complete
// Craft quests: CraftingSystem.OnCraftSuccess → match recipe
// Capture quests: TamerSystem.OnPetCaptured → match pet type
```

### 10.2 Events

```
OnQuestAccepted(QuestData)                            → UI quest log update
OnObjectiveProgress(QuestData, int objectiveIndex, int current, int required)
OnQuestCompleted(QuestData)                            → UI "Hoàn thành!" + reward popup
OnQuestTurnedIn(QuestData)                             → UI reward claimed
```

### 10.3 QuestData SO

```csharp
[CreateAssetMenu(menuName = "Game/Quest Data")]
[BillTitle("Quest", "Nhiệm vụ")]
public class QuestData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string questID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillLabelText("Desc Key (Loc)")] public string descKey;
    [BillEnumToggleButtons] public QuestType questType;
    [BillPreviewField] public Sprite icon;

    [BillBoxGroup("Requirements")]
    public int requiredLevel;
    public QuestData[] prerequisiteQuests;   // phải hoàn thành trước

    [BillBoxGroup("Objectives")]
    [BillTableList]
    public QuestObjective[] objectives;

    [BillBoxGroup("Rewards")]
    public float expReward;
    public int goldReward;
    public int spReward;                    // bonus skill points
    [BillTableList]
    public QuestRewardItem[] itemRewards;

    [BillBoxGroup("Repeatable")]
    public bool isRepeatable;
    [BillShowIf("isRepeatable")]
    public float repeatCooldown;            // seconds (daily = 86400)
}

[Serializable]
public class QuestObjective
{
    [BillEnumToggleButtons] public ObjectiveType type;
    [BillLabelText("Desc Key (Loc)")] public string descKey;    // "Tiêu diệt 5 Sói Hoang"

    // Kill
    [BillShowIf("type", ObjectiveType.Kill)]
    public EnemyData targetEnemy;
    // Collect
    [BillShowIf("type", ObjectiveType.Collect)]
    public ItemData targetItem;
    // Talk
    [BillShowIf("type", ObjectiveType.Talk)]
    public NPCData targetNPC;
    // Reach
    [BillShowIf("type", ObjectiveType.Reach)]
    public string targetZoneID;
    // Craft
    [BillShowIf("type", ObjectiveType.Craft)]
    public RecipeData targetRecipe;
    // Capture
    [BillShowIf("type", ObjectiveType.Capture)]
    public PetData targetPet;

    public int requiredCount = 1;
}

[Serializable]
public class QuestRewardItem
{
    public ItemData item;
    public int quantity;
}
```

### 10.4 QuestInstance (runtime state)

```csharp
[Serializable]
public class QuestInstance
{
    public QuestData Data;
    public QuestState State;
    public int[] objectiveProgress;         // index maps to Data.objectives[]
    public float acceptedTime;              // for daily/weekly reset
    // SpacetimeDB-ready: fully serializable
}
```

---

## 11. DIALOGUE SYSTEM

### 11.1 Public API

```csharp
Game.Dialogue.StartDialogue(dialogueData)     → void (show DialoguePanel)
Game.Dialogue.Advance()                        → void (next node)
Game.Dialogue.SelectChoice(int index)          → void
Game.Dialogue.IsActive                         → bool
Game.Dialogue.CurrentNode                      → DialogueNode
```

### 11.2 Events

```
OnDialogueStart(DialogueData)
OnNodeChanged(DialogueNode)
OnChoiceSelected(int choiceIndex)
OnDialogueEnd(DialogueData)
```

### 11.3 DialogueData SO

```csharp
[CreateAssetMenu(menuName = "Game/Dialogue Data")]
[BillTitle("Dialogue", "Hội thoại")]
public class DialogueData : ScriptableObject
{
    public string dialogueID;
    [BillTableList]
    public DialogueNode[] nodes;
}

[Serializable]
public class DialogueNode
{
    public int nodeID;
    [BillEnumToggleButtons]
    public DialogueNodeType type;           // Text, Choice, Condition, Event

    // Text node
    [BillShowIf("type", DialogueNodeType.Text)]
    public string speakerNameKey;
    [BillShowIf("type", DialogueNodeType.Text)]
    public Sprite speakerPortrait;
    [BillShowIf("type", DialogueNodeType.Text)]
    public string textKey;                  // localization key
    [BillShowIf("type", DialogueNodeType.Text)]
    public int nextNodeID = -1;             // -1 = end dialogue

    // Choice node
    [BillShowIf("type", DialogueNodeType.Choice)]
    [BillTableList]
    public DialogueChoice[] choices;

    // Condition node
    [BillShowIf("type", DialogueNodeType.Condition)]
    public string conditionField;           // "quest.wolf_hunt.completed", "level >= 10"
    [BillShowIf("type", DialogueNodeType.Condition)]
    public int trueNodeID;
    [BillShowIf("type", DialogueNodeType.Condition)]
    public int falseNodeID;

    // Event node (trigger game action)
    [BillShowIf("type", DialogueNodeType.Event)]
    public string eventName;                // "give_quest", "open_shop", "give_item"
    [BillShowIf("type", DialogueNodeType.Event)]
    public string eventParam;               // quest ID, shop ID, item ID
    [BillShowIf("type", DialogueNodeType.Event)]
    public int afterEventNodeID;
}

[Serializable]
public class DialogueChoice
{
    public string textKey;                  // "Chấp nhận nhiệm vụ" / "Từ chối"
    public int targetNodeID;
    public string condition;                // optional: only show if condition met
}
```

---

## 12. UI / HUD ARCHITECTURE

Dùng `Bill.UI` BasePanel pattern. Tất cả panel kế thừa BasePanel.

### 12.1 Panel List

```
ALWAYS VISIBLE (HUD):
  HUDPanel            → HP/MP/Chi/Stamina bars, skill bar (4+2 slots), 
                         minimap, target info, combo counter, EXP bar, gold

TOGGLE PANELS:
  InventoryPanel      → 30 slot grid, drag-drop, right-click use/equip, gold display
  EquipmentPanel      → 8 slots (head/body/legs/feet/mainhand/offhand/acc1/acc2), stat preview
  SkillTreePanel      → 14 tree tabs, skill nodes with tier layout, SP counter, drag to SkillBar
  QuestPanel          → Active/Completed tabs, objective checklist, rewards preview
  PetPanel            → Pet storage list, active pet stats, feed/fuse buttons
  MapPanel            → Zone map with portal markers, spawn zones

POPUP PANELS:
  ShopPanel           → Buy/sell grid, item comparison tooltip
  CraftingPanel       → Recipe list, material check, craft button, success rate
  DialoguePanel       → Speaker portrait, text box, choice buttons
  SettingsPanel       → Language, audio, graphics, controls, keybindings
  DeathPanel          → "Bạn đã gục ngã!", respawn options, gold penalty

OVERLAY:
  DamagePopup         → Floating numbers, pooled via Bill.Pool
  LootPopup           → Item drop notification (top-right, fade out)
  LevelUpOverlay      → Full-screen flash + "LEVEL UP!" text
  ComboCounter        → "COMBO x5!" center screen
  BossHPBar           → Top of screen, boss name + HP bar
```

### 12.2 HUD Layout

```
┌──────────────────────────────────────────────────────┐
│ [Minimap]                              [Gold: 12,500] │
│                                        [Quest Tracker] │
│                                                        │
│                                                        │
│                    [Combo x3!]                          │
│                   [DamagePopup]                         │
│                                                        │
│                                                        │
│                                          [Target Info] │
│                                          [Enemy HP Bar]│
│                                          [Enemy Name]  │
│                                                        │
│ [HP ████████░░]  100/150                               │
│ [MP ████░░░░░░]   40/100                               │
│ [Chi████████░░]   80/100                               │
│ [Stam██████░░░]   60/100                               │
│                                                        │
│ [EXP ███████░░░░░░░░░░░░░░] Lv.25  3,200/8,944       │
│                                                        │
│      [Skill1] [Skill2] [Skill3] [Skill4]              │
│       (Q)      (W)      (E)      (R)                  │
│                              [Block(RMB)] [Parry(tap)] │
└──────────────────────────────────────────────────────┘
```

### 12.3 Bill.UI Integration

```csharp
// Show panel:
Bill.UI.Show<InventoryPanel>();
Bill.UI.Show<ShopPanel>(shopData);   // with init data

// Hide:
Bill.UI.Hide<InventoryPanel>();

// Toggle:
Bill.UI.Toggle<InventoryPanel>();    // I key

// Keybindings:
// I = Inventory, K = Skills, J = Quest, P = Pet, M = Map, ESC = Settings
// Tab = Target lock, Q/W/E/R = Skill 1-4, RMB hold = Block, RMB tap = Parry
```

---

## 13. SAVE / LOAD SYSTEM

Dùng `Bill.Save` cho persistence. Tất cả game state serialize thành 1 SaveData object.

### 13.1 Public API

```csharp
Game.SaveLoad.Save(slotIndex)             → void
Game.SaveLoad.Load(slotIndex)             → void
Game.SaveLoad.AutoSave()                  → void
Game.SaveLoad.HasSave(slotIndex)          → bool
Game.SaveLoad.GetSaveInfo(slotIndex)      → SaveInfo (timestamp, level, playtime)
Game.SaveLoad.DeleteSave(slotIndex)       → void
```

### 13.2 SaveData Structure

```csharp
[Serializable]
public class SaveData
{
    // Meta
    public string saveVersion = "1.0";
    public float playTime;                  // total seconds played
    public string lastSaveTime;             // ISO 8601

    // Player
    public int level;
    public float currentExp;
    public int unspentStatPoints;
    public int unspentSkillPoints;
    public int[] allocatedStats;            // index = StatType, value = points spent

    // Resources
    public float currentHP, currentMana, currentStamina, currentChi;

    // Inventory
    public SavedItemStack[] inventorySlots; // 30 slots
    public int gold;

    // Equipment
    public string[] equippedItemIDs;        // index = EquipSlot, value = itemID (null = empty)

    // Skills
    public SavedSkillState[] learnedSkills; // skillID + level
    public string[] skillBarSlots;          // 4 slots, skillID (null = empty)

    // Quests
    public SavedQuestState[] quests;        // questID + state + progress[]

    // Pets
    public SavedPetState[] pets;

    // World
    public string currentZoneID;
    public float[] playerPosition;          // x, y, z
    public float[] playerRotation;          // euler

    // Settings
    public string language = "vi";
    public float bgmVolume = 0.7f;
    public float sfxVolume = 1f;
}

[Serializable]
public struct SavedItemStack
{
    public string itemID;           // reference to SO via itemID lookup
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

[Serializable]
public struct SavedPetState
{
    public string petID;
    public string nickname;
    public int level;
    public float currentExp;
    public int bond;
}
```

### 13.3 Auto-Save Triggers

```
Auto-save khi:
  → Level up
  → Zone transition (enter new zone)
  → Equipment change (equip/unequip)
  → Quest completed
  → Every 5 minutes (configurable)

Save slot:
  Slot 0 = Auto-save (overwrite)
  Slot 1-3 = Manual saves

SpacetimeDB future: save = server state. Bill.Save chỉ cache local cho offline.
Khi online: SaveLoad.Save() → send state to SpacetimeDB reducer.
Khi offline: SaveLoad.Save() → Bill.Save to local.
```

### 13.4 Item/Skill ID Lookup

```csharp
// ItemDatabase — singleton SO chứa registry tất cả items
[CreateAssetMenu(menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public ItemData[] allItems;
    public WeaponData[] allWeapons;
    public ArmorData[] allArmors;

    private Dictionary<string, ItemData> _lookup;

    public ItemData GetByID(string itemID)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(itemID, out var item) ? item : null;
    }
}

// Tương tự: SkillDatabase, EnemyDatabase, PetDatabase
// Pattern này đảm bảo SO reference qua ID string → serializable → SpacetimeDB ready
```

---

## 14. SCENE / MAP / ZONE SYSTEM

### 14.1 Public API

```csharp
Game.Zone.CurrentZone                         → ZoneData
Game.Zone.TravelTo(zoneData, spawnPointID)    → void (Bill.Scene.Load + fade)
Game.Zone.GetDiscoveredZones()                → List<ZoneData>
Game.Zone.IsZoneDiscovered(zoneData)          → bool
```

### 14.2 Events

```
OnZoneEnter(ZoneData zone)                   → Bill.Audio.PlayBGM(zone.bgm), UI zone name
OnZoneExit(ZoneData zone)                    → cleanup mobs, save
OnPortalUsed(Portal from, Portal to)         → fade transition
OnZoneDiscovered(ZoneData zone)              → UI "Khám phá: Rừng Sói Hoang!"
```

### 14.3 ZoneData SO

```csharp
[CreateAssetMenu(menuName = "Game/Zone Data")]
[BillTitle("Zone", "Khu vực trong game")]
public class ZoneData : ScriptableObject
{
    [BillBoxGroup("Identity")]
    public string zoneID;
    [BillLabelText("Name Key (Loc)")] public string nameKey;
    [BillEnumToggleButtons] public ZoneType type;   // Town, Field, Dungeon, Boss, Arena

    [BillBoxGroup("Scene")]
    public string sceneName;                        // Unity scene name
    public string bgmKey;                           // Bill.Audio BGM key

    [BillBoxGroup("Level")]
    [BillSlider(1, 100)] public int recommendedLevel;
    [BillSlider(1, 100)] public int minLevel;       // 0 = no restriction

    [BillBoxGroup("Spawn Points")]
    [BillTableList]
    public SpawnPoint[] spawnPoints;

    [BillBoxGroup("Connections")]
    [BillTableList]
    public ZoneConnection[] connections;             // portals to other zones
}

[Serializable]
public class SpawnPoint
{
    public string spawnID;                  // "entrance", "boss_room", "checkpoint_1"
    public Vector3 position;
    public float yRotation;
}

[Serializable]
public class ZoneConnection
{
    public ZoneData targetZone;
    public string targetSpawnID;            // where player appears in target zone
    public int requiredLevel;               // 0 = no restriction
    public QuestData requiredQuest;         // null = no quest gate
}
```

### 14.4 Portal Component

```csharp
/// <summary>
/// Attach lên trigger collider trong scene.
/// Player enter → transition to target zone.
/// </summary>
public class Portal : MonoBehaviour
{
    public ZoneData targetZone;
    public string targetSpawnID;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Game.Zone.TravelTo(targetZone, targetSpawnID);
    }
}
```

### 14.5 Zone Transition Flow

```
1. Portal.OnTriggerEnter → Game.Zone.TravelTo(target, spawnID)
2. Game.SaveLoad.AutoSave()
3. Bill.Scene.Load(target.sceneName, fadeColor, fadeDuration)
4. On scene loaded:
   → Player.transform.position = spawnPoint.position
   → Bill.Audio.PlayBGM(target.bgmKey)
   → Fire OnZoneEnter(target)
   → VAT_MobSpawner[] in scene activate (distance-based)
   → UI show zone name fade-in
5. Mark zone as discovered if first visit
```

---

## 15. DEATH & RESPAWN SYSTEM

### 15.1 Public API

```csharp
Game.Death.Die()                              → void (called by HealthSystem.OnDeath)
Game.Death.Respawn(RespawnOption option)       → void
Game.Death.GoldPenalty                         → int (10% of current gold)
```

### 15.2 Death Flow

```
1. HealthSystem.OnDeath fires
2. CombatSM → DeadState (death animation, disable input)
3. Camera: slow zoom out, grayscale filter (2s)
4. Bill.UI.Show<DeathPanel>()
   → "Bạn đã gục ngã!"
   → Gold penalty: 10% of current gold (shown)
   → Options:
     A) "Hồi sinh tại thị trấn" (free)
        → Respawn at last town visited, full HP/MP
     B) "Hồi sinh tại chỗ" (costs gold = 5% of total gold, minimum 100)
        → Respawn at death location, 50% HP, no MP
     C) Future MMORPG: "Hồi sinh bằng đá" (premium item)
        → Instant respawn, full HP/MP, no penalty
5. Player chọn → Game.Inv.SpendGold(penalty) → Game.Zone.TravelTo() or reset position
6. Respawn invincibility: 3 seconds (enemies ignore, no damage taken)
```

### 15.3 SpacetimeDB Note

```
Server-authoritative death:
  → Server detects HP ≤ 0 → sets player state = Dead
  → Client receives state change → show DeathPanel
  → Client sends respawn intent → server validates gold → updates position + HP
  → Prevents exploit: client can't skip death penalty
```

---

## 16. FOCUS GAUGE (Katana — Nhẫn Đạo)

### 16.1 Concept

Focus = resource riêng của Katana. Tích khi bình tĩnh, mất khi bị hit.
Katana skills bonus damage dựa trên Focus level.
"Patience = Power" playstyle.

### 16.2 API

```csharp
// Trên PlayerCore, chỉ active khi MainHand = Katana
Game.Focus.Current                           → float (0-100)
Game.Focus.Max                               → float (100)
Game.Focus.Ratio                             → float (0-1)
Game.Focus.GetDamageBonus()                  → float (1.0 ~ 1.5)
```

### 16.3 Mechanics

```
Focus Gain:
  +3/s    while stationary in combat (standing still, locked-on)
  +15     on successful dodge (reward skilled play)
  +25     on successful parry (high reward)
  +5      per auto-attack hit

Focus Loss:
  -2/s    while moving in combat
  -30     on hit taken (punishment for getting hit)
  -ALL    on death
  -5/s    out of combat (decay, but slower than Chi)

Focus Damage Bonus:
  bonus = 1.0 + (currentFocus / maxFocus) * 0.5
  → 0 Focus = 1.0x damage (no bonus)
  → 50 Focus = 1.25x damage
  → 100 Focus = 1.5x damage (50% bonus!)
  → Only applies to Katana tree skills

Counter Stance (Katana tier 3 skill):
  → Enter stance for 1s (costs 30 Focus)
  → If hit during stance: negate damage, deal 200% counter attack
  → If NOT hit: lose Focus, 1s recovery
  → High risk/reward mechanic
```

### 16.4 Implementation

```csharp
public class FocusGauge : MonoBehaviour
{
    [BillProgressBar(0, 100, ColorType.Cyan)]
    [BillReadOnly, BillShowInInspector]
    public float Current { get; private set; }

    public float Max => 100f;
    public float Ratio => Current / Max;
    public bool IsActive => Game.Weapon.MainHandWeapon?.Type == WeaponType.Katana;

    public event Action<float> OnFocusChanged;

    public float GetDamageBonus() => IsActive ? 1f + Ratio * 0.5f : 1f;

    // Called by SkillExecuteState for Katana skills:
    // rawDamage *= Game.Focus.GetDamageBonus();
}
```

### 16.5 UI

```
Circular gauge around reticle when Katana equipped
  → Empty = dark ring
  → Full = glowing cyan ring + particle effect
  → Pulse animation when > 80 Focus
```

---

## 17. WEAPON ENHANCEMENT SYSTEM

### 17.1 API

```csharp
Game.Enhance.GetLevel(weaponInstance)          → int (0-10)
Game.Enhance.GetSuccessRate(weaponInstance)     → float
Game.Enhance.TryEnhance(weaponInstance, stoneItem)  → EnhanceResult
Game.Enhance.GetStatBonus(weaponInstance)       → float multiplier (1.0 + 0.05 * level)
```

### 17.2 Enhancement Data

```
Level   Success%   Fail Penalty         Stone Cost
+1      100%       -                    Enhancement Stone x1
+2      100%       -                    Enhancement Stone x1
+3      100%       -                    Enhancement Stone x2
+4       80%       Nothing lost         Enhancement Stone x2
+5       80%       Nothing lost         Enhancement Stone x3
+6       60%       Nothing lost         Enhancement Stone x3
+7       40%       Downgrade to +6      Enhancement Stone x5
+8       30%       Downgrade to +7      Enhancement Stone x5
+9       20%       Downgrade to +8      Enhancement Stone x8
+10      10%       Downgrade to +9      Enhancement Stone x10

Protection Stone: prevents downgrade on fail (consumed)
Blacksmith skill level: +2% per level (max +20% at skill level 10)
```

### 17.3 Stat Scaling

```
Enhanced weapon stats = baseStats * (1 + 0.05 * enhanceLevel)
  → +5 weapon = 25% stronger
  → +10 weapon = 50% stronger

Visual glow:
  +1~+3:  faint shimmer
  +4~+6:  medium glow (blue)
  +7~+9:  strong glow (purple)
  +10:    legendary glow (gold) + particle trail
```

### 17.4 WeaponInstance (runtime, enhanced weapon)

```csharp
[Serializable]
public class WeaponInstance
{
    public WeaponData BaseData;
    public int enhanceLevel;

    public float EffectiveDamage => BaseData.BaseDamage * (1f + 0.05f * enhanceLevel);
    public float EffectiveAttackSpeed => BaseData.AttackSpeedModifier;  // enhance doesn't affect speed

    // Display name: Loc.Get(BaseData.nameKey) + (enhanceLevel > 0 ? $" +{enhanceLevel}" : "")
    // SpacetimeDB: weaponID + enhanceLevel = minimal serialization
}
```

---

## 18. SPACETIMEDB MMORPG MIGRATION PATH

### 18.1 Architecture Strategy

```
HIỆN TẠI (Offline Single-Player):
  Game.* facade → direct component call → local state → local events

TƯƠNG LAI (MMORPG via SpacetimeDB):
  Game.* facade → NetworkService → SpacetimeDB reducer call → server validates
                                 → subscription callback → local state update → events

GIỮ NGUYÊN: Game.* API interface. Player code KHÔNG THAY ĐỔI.
THAY ĐỔI:   Implementation bên trong mỗi system thêm network layer.
```

### 18.2 Design Principles (áp dụng NGAY)

```
P1: Mọi state mutation qua single entry point
    ✅ Game.Inv.AddItem()      — không trực tiếp sửa inventory array
    ✅ Game.Stats.SetBaseStat() — không trực tiếp sửa stat value
    → Migration: wrap entry point với network call

P2: ID-based references (không direct SO reference cho persistent data)
    ✅ SaveData dùng itemID string, skillID string
    ✅ Network gửi ID, server lookup
    → Migration: gửi ID qua network, server resolve

P3: Events cho mọi state change
    ✅ OnSlotChanged, OnLevelUp, OnEquipped...
    → Migration: server broadcast → client event fire

P4: Deterministic formulas
    ✅ Damage formula, EXP formula = pure function of inputs
    → Migration: same formula chạy cả client (prediction) và server (authority)

P5: State serializable
    ✅ SaveData, PetInstance, QuestInstance = [Serializable]
    → Migration: serialize → SpacetimeDB table row
```

### 18.3 SpacetimeDB Table Design (Future Reference)

```sql
-- Player core
table players {
    identity: Identity,     -- SpacetimeDB auth
    username: String,
    level: u32,
    exp: f32,
    gold: u32,
    zone_id: String,
    position_x: f32, position_y: f32, position_z: f32,
    hp: f32, mp: f32, stamina: f32, chi: f32,
    stat_points: [u32; 7],  -- STR/INT/AGI/DEX/VIT/LUK/TECH allocated
}

-- Inventory (1 row per slot)
table inventory_slots {
    player_id: Identity,
    slot_index: u32,
    item_id: String,
    quantity: u32,
    enhance_level: u32,     -- for weapons
}

-- Equipment
table equipment {
    player_id: Identity,
    slot: u8,               -- EquipSlot enum
    item_id: String,
    enhance_level: u32,
}

-- Skills
table learned_skills {
    player_id: Identity,
    skill_id: String,
    level: u32,
}

table skill_bar {
    player_id: Identity,
    slot: u8,
    skill_id: String,
}

-- Quests
table quest_progress {
    player_id: Identity,
    quest_id: String,
    state: u8,
    objective_progress: Vec<u32>,
}

-- Pets
table pets {
    pet_instance_id: u64,
    owner_id: Identity,
    pet_data_id: String,
    nickname: String,
    level: u32,
    exp: f32,
    bond: u32,
}

-- Zone instances (MMORPG channels)
table zone_instances {
    instance_id: u64,
    zone_id: String,
    player_count: u32,
    max_players: u32,       -- 50 per instance
}
```

### 18.4 Reducer Examples (Future Reference)

```rust
// Server-side (SpacetimeDB module in Rust):

#[spacetimedb::reducer]
fn use_skill(ctx: &ReducerContext, slot: u8) -> Result<(), String> {
    let player = get_player(ctx.sender)?;
    let skill = get_skill_bar(ctx.sender, slot)?;
    let skill_data = lookup_skill(&skill.skill_id)?;

    // Gate checks (server authoritative)
    if player.mp < skill_data.mp_cost { return Err("Not enough MP"); }
    if player.hp <= 0.0 { return Err("Dead"); }

    // Consume resources
    update_player_mp(ctx.sender, player.mp - skill_data.mp_cost);

    // Calculate damage (deterministic formula, same as client)
    let damage = calculate_skill_damage(&player, &skill_data);

    // Find targets (server knows all positions)
    let targets = find_targets_in_range(player.position, skill_data.range, skill_data.target_type);

    // Apply damage to each target
    for target in targets {
        apply_damage(target, damage);
    }

    // Client receives state updates via subscriptions automatically
    Ok(())
}
```

### 18.5 Migration Checklist (khi bắt đầu MMORPG)

```
Phase M1: Network Foundation
  □ Add SpacetimeDB SDK to Unity project
  □ Create IGameService interface for each system
  □ Game.* facade resolve: offline → LocalService, online → NetworkService
  □ Authentication flow (SpacetimeDB Identity)

Phase M2: Core Systems Online
  □ Player position sync (client prediction + server correction)
  □ Inventory + Equipment (server authoritative)
  □ Level + Skills (server authoritative)
  □ Other players visible (subscription to nearby players)

Phase M3: Combat Online
  □ Damage calculation (server authoritative, client prediction for feel)
  □ Enemy sync (server spawns + controls AI, client renders)
  □ Skill execution (client sends intent, server validates + broadcasts result)
  □ Loot (server rolls, client sees drop)

Phase M4: Social
  □ Chat (SpacetimeDB table)
  □ Party system
  □ Trade (pet marketplace, item trade)
  □ Guild

Phase M5: Economy
  □ Shop (server prices, anti-exploit)
  □ Crafting (server validates materials)
  □ Enhancement (server rolls success)
  □ Auction house
```

---

## 19. UPDATED COMPLETE EVENT MAP

```
EVENT                                  FIRED BY              SUBSCRIBED BY
──────────────────────────────────────────────────────────────────────────────

HealthSystem
  OnResourceChanged(type,cur,max)      HealthSystem          HUDPanel bars
  OnDamageTaken(float)                 HealthSystem          Camera shake, Bill.Audio
  OnDeath                              HealthSystem          CombatSM, DeathSystem, Controller
  OnHealReceived(float)                HealthSystem          HUDPanel heal popup

PlayerDamageHandler
  OnDamageDealt(target, result)        PlayerDamageHandler   DamagePopup, ComboTracker, Chi gain
  OnDamageTaken(DamageResult)          PlayerDamageHandler   DamagePopup, Camera shake

CharacterStats
  OnStatChanged(type, old, new)        CharacterStats        HUDPanel, EquipmentPanel

LevelSystem
  OnLevelUp(int)                       LevelSystem           Bill.Events broadcast, HUDPanel, Bill.Audio
  OnExpGained(float, float)            LevelSystem           HUDPanel exp bar
  OnStatPointSpent(StatType)           LevelSystem           EquipmentPanel

Inventory
  OnSlotChanged(int, ItemStack)        Inventory             InventoryPanel
  OnItemAdded(ItemData, int)           Inventory             LootPopup, QuestTracker
  OnItemRemoved(ItemData, int)         Inventory             InventoryPanel
  OnGoldChanged(int)                   Inventory             HUDPanel gold display
  OnInventoryFull                      Inventory             HUDPanel warning msg

EquipmentSystem
  OnEquipped(EquipSlot, ItemData)      EquipmentSystem       EquipmentPanel, WeaponHandler
  OnUnequipped(EquipSlot, ItemData)    EquipmentSystem       EquipmentPanel, WeaponHandler

WeaponHandler
  OnWeaponChanged(IWeapon, slot)       WeaponHandler         SkillBar (weapon validation), FocusGauge

LockOnSystem
  OnTargetLocked(IDamageable)          LockOnSystem          PlayerController, Camera, HUDPanel
  OnTargetLost                         LockOnSystem          PlayerController, Camera, HUDPanel

PlayerSkillBook
  OnSkillLearned(SkillData, int)       PlayerSkillBook       SkillTreePanel
  OnSkillPointsChanged(int)            PlayerSkillBook       SkillTreePanel SP counter

SkillBar
  OnSkillBarChanged(int, SkillData)    SkillBar              HUDPanel hotbar
  OnCooldownUpdate(int, float)         SkillBar              HUDPanel cooldown sweep

SkillCaster
  OnSkillCastStart(SkillData)          SkillCaster           HUDPanel cast bar, Bill.Audio
  OnSkillCastComplete(SkillData)       SkillCaster           Bill.Audio, VFX
  OnSkillCastInterrupted(SkillData)    SkillCaster           HUDPanel warning

ComboTracker
  OnComboStart                         ComboTracker          HUDPanel combo display
  OnComboCountChanged(int)             ComboTracker          HUDPanel combo counter
  OnComboEnd                           ComboTracker          HUDPanel hide combo

StatusEffectSystem
  OnEffectApplied(ActiveStatusEffect)  StatusEffectSystem    HUDPanel buff icons
  OnEffectRemoved(ActiveStatusEffect)  StatusEffectSystem    HUDPanel buff icons
  OnEffectTick(ActiveStatusEffect)     StatusEffectSystem    DamagePopup (DoT numbers)

FocusGauge
  OnFocusChanged(float)                FocusGauge            HUDPanel focus ring (Katana only)

EnemyBase
  OnDeath                              EnemyBase             LootSystem, PackManager, LockOn, QuestTracker
  OnHPChanged(float)                   EnemyBase             HUDPanel enemy HP bar
  OnDamageTaken(float)                 EnemyBase             EnemyAI (aggro), DamagePopup

PackManager
  OnThreatChanged(ThreatLevel)         PackManager           (future UI zone danger)
  OnPackWiped                          PackManager           VAT_MobSpawner (respawn trigger)

TamerSystem
  OnPetCaptured(PetInstance)           TamerSystem           PetPanel, Bill.Audio
  OnCaptureFailed                      TamerSystem           HUDPanel message
  OnPetSummoned(PetInstance)           TamerSystem           PetPanel
  OnPetLevelUp(PetInstance, int)       TamerSystem           PetPanel

CraftingSystem
  OnCraftSuccess(RecipeData, ItemData) CraftingSystem        CraftingPanel, Bill.Audio
  OnCraftFail(RecipeData)              CraftingSystem        CraftingPanel, Bill.Audio
  OnEnhanceResult(WeaponInstance, EnhanceResult) CraftingSystem  CraftingPanel, VFX

QuestTracker
  OnQuestAccepted(QuestData)           QuestTracker          QuestPanel, HUDPanel quest tracker
  OnObjectiveProgress(QuestData,int,int,int) QuestTracker    QuestPanel, HUDPanel
  OnQuestCompleted(QuestData)          QuestTracker          QuestPanel, Bill.Audio, Bill.Events
  OnQuestTurnedIn(QuestData)           QuestTracker          QuestPanel, LootPopup (rewards)

DialogueSystem
  OnDialogueStart(DialogueData)        DialogueSystem        DialoguePanel
  OnNodeChanged(DialogueNode)          DialogueSystem        DialoguePanel
  OnDialogueEnd(DialogueData)          DialogueSystem        DialoguePanel, (trigger quest/shop)

DeathSystem
  OnPlayerDied                         DeathSystem           DeathPanel, Bill.Audio
  OnPlayerRespawned(RespawnOption)     DeathSystem           Camera reset, HUDPanel

ZoneSystem
  OnZoneEnter(ZoneData)                ZoneSystem            Bill.Audio.PlayBGM, HUDPanel zone name
  OnZoneExit(ZoneData)                 ZoneSystem            SaveLoad.AutoSave
  OnZoneDiscovered(ZoneData)           ZoneSystem            MapPanel, HUDPanel notification

LocalizationService
  OnLanguageChanged(string)            LocalizationService   ALL LocalizedText components

Bill.Events (cross-system broadcasts)
  GameEvent.PlayerLevelUp              LevelSystem           Multiple systems need to react
  GameEvent.BossDefeated               EnemyBase (boss)      Multiple systems, cutscene trigger
  GameEvent.ZoneFirstVisit             ZoneSystem            Achievement, tutorial triggers
```

---

## 20. UPDATED COMPLETE FILE INVENTORY

### A. EXISTING FILES (keep/update)

```
KEEP     Camera/CameraController.cs
KEEP     Core/Animation/AnimationController.cs
UPDATE   Core/Combat/AutoAttackSystem.cs              (+MP/Chi recovery, +Focus gain)
KEEP     Core/Combat/CombatLocomotion.cs
KEEP     Core/Combat/DamagePipeline.cs
UPDATE   Core/Combat/EnemyBase.cs                     (+Commands, +PooledObject, +VAT, +BillInspector)
UPDATE   Core/Combat/Hitbox/HitboxManager.cs          (+IDamageDealer parent lookup)
KEEP     Core/Combat/LockOnSystem.cs
UPDATE   Core/Combat/StateMachine/CombatState.cs      (keep)
UPDATE   Core/Combat/StateMachine/CombatStateMachine.cs (+skill states, +helpers, -Block/Parry/Guard)
UPDATE   Core/Combat/StateMachine/CombatStates.cs     (-Block/Parry/Guard states, +skill input check)
UPDATE   Core/Health/HealthSystem.cs                  (+Chi, +ResourceType)
KEEP     Core/Locomotion/LocomotionState.cs
KEEP     Core/Locomotion/LocomotionStateMachine.cs
KEEP     Core/Locomotion/LocomotionStates.cs
UPDATE   Core/Player/PlayerController.cs              (+wire new systems, +weapon visual)
KEEP     Core/Stats/CharacterStats.cs
KEEP     Data/WeaponData.cs
DEPREC   Core/Combat/PlayerCombatController.cs        → _Deprecated/
DEPREC   Input/CombatInputHandler.cs                  → _Deprecated/
UPDATE   Input/PlayerInputHandler.cs                  (+skill 1-4, +block/parry input)
KEEP     Interfaces/IAnimationController.cs
UPDATE   Interfaces/ICombat.cs                        (+ECombatState update)
KEEP     Interfaces/IStatProvider.cs
KEEP     Interfaces/IWeapon.cs
KEEP     Weapons/WeaponHandler.cs
KEEP     Editor/RPGModularSetupWizard.cs
```

### B. NEW FILES — Foundation

```
NEW  Core/Game.cs                                     (~60 lines)
NEW  Core/Player/PlayerCore.cs                        (~80 lines)
NEW  Enums/GameEnums.cs                               (~120 lines)
NEW  Data/SharedDataTypes.cs                           (~80 lines)
NEW  Data/ItemDatabase.cs                              (~50 lines) — ID lookup registry
NEW  Data/SkillDatabase.cs                             (~40 lines)
```

### C. NEW FILES — Localization

```
NEW  Core/Localization/LocalizationService.cs          (~150 lines)
NEW  Core/Localization/Loc.cs                          (~30 lines)
NEW  Core/Localization/LocalizedText.cs                (~40 lines)
NEW  Data/LocalizationConfig.cs                        (~30 lines)
NEW  Resources/Localization/vi.json                    (growing)
NEW  Resources/Localization/en.json                    (growing)
```

### D. NEW FILES — Combat Fixes

```
NEW  Core/Combat/PlayerDamageHandler.cs                (~160 lines)
NEW  Core/Combat/States/SkillChargeState.cs            (~70 lines)
NEW  Core/Combat/States/SkillExecuteState.cs           (~150 lines)
NEW  Core/Combat/States/ComboReadyState.cs             (~60 lines)
NEW  Core/Combat/FocusGauge.cs                         (~80 lines)
```

### E. NEW FILES — Skill System

```
NEW  Data/SkillData.cs                                 (~120 lines)
NEW  Data/SkillTreeData.cs                             (~40 lines)
NEW  Core/Skill/PlayerSkillBook.cs                     (~160 lines)
NEW  Core/Skill/SkillBar.cs                            (~120 lines)
NEW  Core/Skill/SkillCaster.cs                         (~220 lines)
NEW  Core/Skill/ComboTracker.cs                        (~90 lines)
```

### F. NEW FILES — Enemy + AI

```
NEW  Core/AI/PackManager.cs                            (~280 lines)
NEW  Core/AI/EnemyAI.cs                                (~350 lines)
NEW  Core/AI/VAT_MobSpawner.cs                         (~220 lines)
NEW  Data/EnemyData.cs                                 (~80 lines)
```

### G. NEW FILES — Inventory + Equipment

```
NEW  Core/Inventory/Inventory.cs                       (~220 lines)
NEW  Core/Inventory/EquipmentSystem.cs                 (~170 lines)
NEW  Data/ItemData.cs                                  (~90 lines)
NEW  Data/ArmorData.cs                                 (~60 lines)
```

### H. NEW FILES — Level + Loot + Status

```
NEW  Core/LevelSystem/LevelSystem.cs                   (~130 lines)
NEW  Core/Loot/LootSystem.cs                           (~180 lines)
NEW  Data/LootTable.cs                                 (~50 lines)
NEW  Core/StatusEffect/StatusEffectSystem.cs            (~220 lines)
NEW  Data/StatusEffectData.cs                           (~70 lines)
```

### I. NEW FILES — Quest + Dialogue + NPC

```
NEW  Core/Quest/QuestTracker.cs                        (~200 lines)
NEW  Data/QuestData.cs                                 (~80 lines)
NEW  Core/Dialogue/DialogueSystem.cs                   (~120 lines)
NEW  Data/DialogueData.cs                              (~60 lines)
NEW  Data/NPCData.cs                                   (~40 lines)
NEW  Data/ShopData.cs                                  (~40 lines)
NEW  Core/NPC/ShopService.cs                           (~100 lines)
NEW  Core/NPC/NPCInteraction.cs                        (~60 lines)
```

### J. NEW FILES — Crafting + Enhancement

```
NEW  Core/Crafting/CraftingSystem.cs                   (~180 lines)
NEW  Core/Crafting/WeaponEnhancement.cs                (~120 lines)
NEW  Data/RecipeData.cs                                (~60 lines)
```

### K. NEW FILES — Tamer

```
NEW  Core/Tamer/TamerSystem.cs                         (~250 lines)
NEW  Core/Tamer/PetAI.cs                               (~150 lines)
NEW  Data/PetData.cs                                   (~60 lines)
```

### L. NEW FILES — Systems

```
NEW  Core/Death/DeathSystem.cs                         (~100 lines)
NEW  Core/Zone/ZoneSystem.cs                           (~120 lines)
NEW  Core/Zone/Portal.cs                               (~30 lines)
NEW  Data/ZoneData.cs                                  (~50 lines)
NEW  Core/SaveLoad/SaveLoadSystem.cs                   (~180 lines)
NEW  Core/SaveLoad/SaveData.cs                         (~80 lines)
NEW  Weapons/WeaponVisualHandler.cs                    (~120 lines)
NEW  Weapons/WeaponInstance.cs                         (~40 lines)
```

### M. NEW FILES — UI Panels (Bill.UI)

```
NEW  UI/HUDPanel.cs                                    (~200 lines)
NEW  UI/InventoryPanel.cs                              (~150 lines)
NEW  UI/EquipmentPanel.cs                              (~120 lines)
NEW  UI/SkillTreePanel.cs                              (~180 lines)
NEW  UI/QuestPanel.cs                                  (~120 lines)
NEW  UI/DialoguePanel.cs                               (~80 lines)
NEW  UI/ShopPanel.cs                                   (~120 lines)
NEW  UI/CraftingPanel.cs                               (~100 lines)
NEW  UI/PetPanel.cs                                    (~100 lines)
NEW  UI/DeathPanel.cs                                  (~60 lines)
NEW  UI/SettingsPanel.cs                               (~80 lines)
NEW  UI/DamagePopup.cs                                 (~80 lines)
NEW  UI/LootPopup.cs                                   (~60 lines)
NEW  UI/MapPanel.cs                                    (~100 lines)
```

### SUMMARY

```
Files keep as-is:        13
Files to update:         11
Files to deprecate:       2
Files to create:         ~75
──────────────────────────
Total files in project:  ~99 C# files
Estimated new code:      ~7,500 lines
```

---

## 21. UPDATED BUILD ORDER

```
═══════════════════════════════════════════════════════
 PHASE 0: FOUNDATION (phải làm trước mọi thứ)
═══════════════════════════════════════════════════════

Step 0.1: Enums + Shared Data Types
  → GameEnums.cs, SharedDataTypes.cs
  → Update ICombat.cs (ECombatState enum)
  → Mọi file khác depend vào enums

Step 0.2: Localization
  → LocalizationService.cs, Loc.cs, LocalizedText.cs, LocalizationConfig.cs
  → vi.json, en.json (skeleton — fill keys as systems are built)
  → Mọi SO depend vào localization keys

Step 0.3: Game.cs + PlayerCore.cs
  → Static facade + hub component
  → Mọi system access qua đây

Step 0.4: Item/Skill/Enemy Database registries
  → ItemDatabase.cs, SkillDatabase.cs
  → ID-based lookup cho Save/Load + SpacetimeDB

═══════════════════════════════════════════════════════
 PHASE 1: CRITICAL FIXES (game phải chạy được)
═══════════════════════════════════════════════════════

Step 1.1: PlayerDamageHandler.cs (NEW)
  → Implement IDamageDealer + IDamageable + ITargetLockable
  → Nối damage flow

Step 1.2: HitboxManager.cs (UPDATE)
  → GetComponentInParent<IDamageDealer>() fallback
  → Fire OnDamageDealt

Step 1.3: EnemyBase.cs (UPDATE)
  → Inherit PooledObject
  → TakeDamage(float), PerformDamageCheck()
  → VAT integration
  → Command methods cho PackManager

Step 1.4: CombatStates.cs (UPDATE)
  → Bỏ BlockingState, ParrySuccessState, GuardBreakState
  → CombatEngagedState.Tick() + skill input check

Step 1.5: CombatStateMachine.cs (UPDATE)
  → Register new states, remove old states
  → Update ECombatState references

Step 1.6: Deprecate old files
  → Move PlayerCombatController.cs → _Deprecated/
  → Move CombatInputHandler.cs → _Deprecated/

═══════════════════════════════════════════════════════
 PHASE 2: CORE SYSTEMS (build order matters)
═══════════════════════════════════════════════════════

Step 2.1: HealthSystem.cs (UPDATE)
  → Add Chi as 4th resource
  → HasChi, TryConsumeChi, ModifyChi
  → Chi decay out of combat

Step 2.2: Inventory + Equipment (parallel OK)
  → Inventory.cs, EquipmentSystem.cs
  → ItemData.cs, ArmorData.cs
  → WeaponInstance.cs (enhanced weapons)

Step 2.3: LevelSystem.cs
  → EXP formula, stat points, skill points

Step 2.4: StatusEffectSystem.cs + StatusEffectData.cs

Step 2.5: LootSystem.cs + LootTable.cs
  → Wire EXP/Gold from enemy death

Step 2.6: WeaponVisualHandler.cs

═══════════════════════════════════════════════════════
 PHASE 3: SKILL SYSTEM (depends on Phase 2)
═══════════════════════════════════════════════════════

Step 3.1: SkillData.cs + SkillTreeData.cs

Step 3.2: PlayerSkillBook.cs
  → Learn/upgrade, SP management, passive bonuses

Step 3.3: SkillBar.cs
  → 4 active + 2 default (block/parry), weapon validation

Step 3.4: SkillCaster.cs
  → Cast flow, gate checks, resource consume

Step 3.5: ComboTracker.cs
  → Combo count, speed/damage bonus

Step 3.6: Combat States (NEW)
  → SkillChargeState.cs, SkillExecuteState.cs, ComboReadyState.cs

Step 3.7: FocusGauge.cs (Katana)

Step 3.8: PlayerInputHandler.cs (UPDATE)
  → Skill inputs 1-4, block/parry input

Step 3.9: AutoAttackSystem.cs (UPDATE)
  → MP recovery per hit, Chi gain per hit, Focus gain

Step 3.10: Default Block/Parry SkillData SOs
  → Create 2 SO assets: default_block, default_parry

═══════════════════════════════════════════════════════
 PHASE 4: ENEMY + AI (depends on Phase 1)
═══════════════════════════════════════════════════════

Step 4.1: EnemyData.cs (SO)
Step 4.2: EnemyAI.cs (9 states)
Step 4.3: PackManager.cs (threat, chase management)
Step 4.4: VAT_MobSpawner.cs (Bill.Pool spawn)

═══════════════════════════════════════════════════════
 PHASE 5: WORLD SYSTEMS (parallel OK)
═══════════════════════════════════════════════════════

Step 5.1: Quest system
  → QuestData.cs, QuestTracker.cs

Step 5.2: Dialogue system
  → DialogueData.cs, DialogueSystem.cs

Step 5.3: NPC + Shop
  → NPCData.cs, ShopData.cs, ShopService.cs, NPCInteraction.cs

Step 5.4: Zone system
  → ZoneData.cs, ZoneSystem.cs, Portal.cs

Step 5.5: Death/Respawn
  → DeathSystem.cs

Step 5.6: Save/Load
  → SaveData.cs, SaveLoadSystem.cs

═══════════════════════════════════════════════════════
 PHASE 6: LIFE SKILLS (depends on Phase 2 Inventory)
═══════════════════════════════════════════════════════

Step 6.1: Crafting system
  → RecipeData.cs, CraftingSystem.cs

Step 6.2: Weapon Enhancement
  → WeaponEnhancement.cs

Step 6.3: Tamer system
  → PetData.cs, TamerSystem.cs, PetAI.cs

═══════════════════════════════════════════════════════
 PHASE 7: UI (depends on all systems)
═══════════════════════════════════════════════════════

Step 7.1: HUDPanel (HP/MP/Chi/Stamina/EXP/Skill bar/Combo/Target)
Step 7.2: InventoryPanel + EquipmentPanel
Step 7.3: SkillTreePanel
Step 7.4: QuestPanel + DialoguePanel
Step 7.5: ShopPanel + CraftingPanel
Step 7.6: PetPanel
Step 7.7: DeathPanel + SettingsPanel
Step 7.8: DamagePopup + LootPopup + MapPanel

═══════════════════════════════════════════════════════
 PHASE 8: INTEGRATION + POLISH
═══════════════════════════════════════════════════════

Step 8.1: PlayerController.cs (UPDATE) — wire everything
Step 8.2: Bill.Audio integration (SFX/BGM cho mọi action)
Step 8.3: VFX (skill effects, enhancement glow, level up)
Step 8.4: Full gameplay loop test
Step 8.5: Balance tuning (damage, EXP, drop rates, costs)

═══════════════════════════════════════════════════════
 PHASE 9: SPACETIMEDB (when ready for MMORPG)
═══════════════════════════════════════════════════════

Step 9.1: SpacetimeDB SDK + auth
Step 9.2: IGameService abstraction layer
Step 9.3: Core systems online (inventory, equip, level, skills)
Step 9.4: Combat online (damage, enemy sync)
Step 9.5: Social (chat, party, trade)
Step 9.6: Economy (shop, craft, auction)
```

---

*End of Architecture Document v2.0 — Complete Resolution*
*Tất cả system đã architect. Tất cả inconsistency đã resolve. SpacetimeDB migration path defined.*
*Ready to implement.*
