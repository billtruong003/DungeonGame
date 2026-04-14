# RPG Modular — Onboarding Guide (Dành cho người mới)

> Tài liệu giúp developer mới hiểu nhanh source code RPG Modular: kiến trúc, quy ước, luồng data, và cách bắt đầu contribute.

---

## MỤC LỤC

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Cấu Trúc Thư Mục](#2-cấu-trúc-thư-mục)
3. [Design Patterns Sử Dụng](#3-design-patterns-sử-dụng)
4. [Cách Truy Cập Systems (Game Facade)](#4-cách-truy-cập-systems-game-facade)
5. [Luồng Data: Từ Asset Đến Runtime](#5-luồng-data-từ-asset-đến-runtime)
6. [Hệ Thống Combat — Luồng Damage](#6-hệ-thống-combat--luồng-damage)
7. [Hệ Thống Stat & Modifier](#7-hệ-thống-stat--modifier)
8. [State Machines](#8-state-machines)
9. [Event System & Cách Lắng Nghe](#9-event-system--cách-lắng-nghe)
10. [ScriptableObject Data Layer](#10-scriptableobject-data-layer)
11. [Interface Map](#11-interface-map)
12. [Enum Reference](#12-enum-reference)
13. [Editor Tools](#13-editor-tools)
14. [Conventions & Quy Ước Code](#14-conventions--quy-ước-code)
15. [Hướng Dẫn Thêm Feature Mới](#15-hướng-dẫn-thêm-feature-mới)
16. [FAQ / Gotchas](#16-faq--gotchas)
17. [Bản Đồ Dependencies](#17-bản-đồ-dependencies)

---

## 1. Tổng Quan Kiến Trúc

RPG Modular là một **component-based RPG framework** cho Unity, thiết kế theo nguyên tắc:

- **Modular**: Mỗi system là 1 MonoBehaviour độc lập, có thể bật/tắt
- **Data-driven**: Game data nằm trong ScriptableObject, không hardcode
- **Event-driven**: Systems giao tiếp qua C# events, không reference trực tiếp
- **Interface-based**: Combat system dùng interfaces (IDamageable, IDamageDealer) để decouple

### Sơ Đồ Kiến Trúc Tổng Quan

```
┌─────────────────────────────────────────────────────┐
│                    GAME (Static Facade)               │
│  Game.Player → Game.Stats → Game.Health → Game.Skill │
└───────────────────────┬─────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│               PLAYER CORE (Hub)                       │
│  Tập hợp tất cả systems trên Player GameObject       │
│                                                       │
│  ┌─────────┐ ┌──────────┐ ┌───────────┐             │
│  │  Stats   │ │  Health   │ │  Combat   │             │
│  │  System  │ │  System   │ │  State    │             │
│  │          │ │           │ │  Machine  │             │
│  └────┬─────┘ └─────┬────┘ └─────┬─────┘             │
│       │             │            │                    │
│  ┌────▼─────┐ ┌─────▼────┐ ┌────▼──────┐            │
│  │ Inventory │ │  Weapon   │ │  Skill    │            │
│  │ Equipment │ │  Handler  │ │  System   │            │
│  └──────────┘ └──────────┘ └───────────┘             │
└──────────────────────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│              SINGLETONS (Scene-level)                 │
│  DialogueSystem, ShopService, LootSystem,            │
│  ZoneSystem, SaveLoadSystem, CraftingSystem,         │
│  WeaponEnhancement, TamerSystem, DeathSystem         │
└──────────────────────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────┐
│              DATA LAYER (ScriptableObjects)           │
│  SkillData, WeaponData, ItemData, EnemyData,         │
│  QuestData, NPCData, DialogueData, StatusEffectData  │
└──────────────────────────────────────────────────────┘
```

---

## 2. Cấu Trúc Thư Mục

```
Assets/Script/RPGModular/
│
├── Core/                      ← TẤT CẢ runtime logic
│   ├── Game.cs                ← Static facade (entry point)
│   ├── Player/
│   │   ├── PlayerCore.cs      ← Hub: auto-find tất cả components
│   │   └── PlayerController.cs ← Mode switching (Explore ↔ Combat)
│   │
│   ├── Stats/
│   │   └── CharacterStats.cs  ← 7 base stats + derived stats + modifiers
│   │
│   ├── Health/
│   │   └── HealthSystem.cs    ← HP/MP/Stamina/Chi resources
│   │
│   ├── Combat/                ← Combat core
│   │   ├── EnemyBase.cs       ← Enemy IDamageable/IDamageDealer
│   │   ├── PlayerDamageHandler.cs ← Player IDamageable/IDamageDealer
│   │   ├── DamagePipeline.cs  ← 5-stage damage processor
│   │   ├── AutoAttackSystem.cs ← Auto combo on lock-on
│   │   ├── LockOnSystem.cs    ← Target management
│   │   ├── FocusGauge.cs      ← Katana unique mechanic
│   │   ├── CombatLocomotion.cs ← Lock-on strafing
│   │   ├── Hitbox/
│   │   │   └── HitboxManager.cs
│   │   ├── StateMachine/
│   │   │   ├── CombatState.cs      ← Base class
│   │   │   ├── CombatStateMachine.cs
│   │   │   └── CombatStates.cs     ← Idle, Engaged, Attacking, Dodge, HitStun, Dead
│   │   └── States/
│   │       ├── ComboReadyState.cs
│   │       ├── SkillChargeState.cs
│   │       └── SkillExecuteState.cs
│   │
│   ├── Skill/
│   │   ├── PlayerSkillBook.cs ← Skill learning (Dict<skillID, level>)
│   │   ├── SkillBar.cs        ← 6 slots (4 active + block + parry)
│   │   ├── SkillCaster.cs     ← Skill execution + damage calc
│   │   └── ComboTracker.cs    ← Combo counter + damage bonus
│   │
│   ├── Inventory/
│   │   ├── Inventory.cs       ← Item storage (30 slots) + Gold
│   │   └── EquipmentSystem.cs ← 8-slot equipment + stat modifiers
│   │
│   ├── AI/
│   │   ├── EnemyAI.cs         ← 9-state AI state machine
│   │   ├── PackManager.cs     ← Multi-enemy coordination
│   │   └── VAT_MobSpawner.cs  ← Spawning + pooling
│   │
│   ├── LevelSystem/
│   │   └── LevelSystem.cs     ← EXP, Level, Stat/Skill points
│   │
│   ├── Quest/
│   │   └── QuestTracker.cs    ← Quest accept/track/complete/turn-in
│   │
│   ├── Dialogue/
│   │   └── DialogueSystem.cs  ← 4-type node dialogue (Singleton)
│   │
│   ├── NPC/
│   │   └── ShopService.cs     ← Buy/Sell (Singleton)
│   │
│   ├── StatusEffect/
│   │   └── StatusEffectSystem.cs ← Buff/debuff + stacking
│   │
│   ├── Loot/
│   │   └── LootSystem.cs      ← Loot roll + reward grant (Singleton)
│   │
│   ├── Death/
│   │   └── DeathSystem.cs     ← Death handling (Singleton)
│   │
│   ├── Crafting/
│   │   ├── CraftingSystem.cs  ← Recipe crafting
│   │   └── WeaponEnhancement.cs ← Weapon upgrade
│   │
│   ├── Tamer/
│   │   └── TamerSystem.cs     ← Pet capture/summon/fuse
│   │
│   ├── Zone/
│   │   ├── ZoneSystem.cs      ← Zone/scene management
│   │   └── Portal.cs          ← Zone transition trigger
│   │
│   ├── SaveLoad/
│   │   ├── SaveData.cs        ← Serializable save state
│   │   └── SaveLoadSystem.cs  ← JSON serialize/deserialize
│   │
│   ├── Animation/
│   │   └── AnimationController.cs ← Animation phases + priority
│   │
│   └── Localization/
│       ├── Loc.cs             ← Static accessor: Loc.Get(key)
│       ├── LocalizationService.cs ← Language management
│       └── LocalizedText.cs   ← UI auto-update component
│
├── Data/                      ← ScriptableObject definitions
│   ├── SkillData.cs           ← [CreateAssetMenu("Game/Skill Data")]
│   ├── WeaponData.cs          ← [CreateAssetMenu("RPG/Weapon Data")]
│   ├── ItemData.cs            ← [CreateAssetMenu("Game/Item Data")]
│   ├── EnemyData.cs           ← [CreateAssetMenu("Game/Enemy Data")]
│   ├── QuestData.cs           ← [CreateAssetMenu("Game/Quest Data")]
│   ├── NPCData.cs             ← [CreateAssetMenu("Game/NPC Data")]
│   ├── DialogueData.cs        ← [CreateAssetMenu("Game/Dialogue Data")]
│   ├── ShopData.cs            ← [CreateAssetMenu("Game/Shop Data")]
│   ├── LootTable.cs           ← [CreateAssetMenu("Game/Loot Table")]
│   ├── StatusEffectData.cs    ← [CreateAssetMenu("Game/Status Effect Data")]
│   ├── PetData.cs             ← [CreateAssetMenu("Game/Pet Data")]
│   ├── RecipeData.cs          ← [CreateAssetMenu("Game/Recipe Data")]
│   ├── ZoneData.cs            ← [CreateAssetMenu("Game/Zone Data")]
│   ├── SkillTreeData.cs       ← [CreateAssetMenu("Game/Skill Tree Data")]
│   ├── SkillDatabase.cs       ← All skills registry
│   ├── ItemDatabase.cs        ← All items registry
│   ├── LocalizationConfig.cs  ← Language settings
│   └── SharedDataTypes.cs     ← StatBonus, StatRequirement, ItemStack, etc.
│
├── Enums/
│   └── GameEnums.cs           ← TẤT CẢ enums (EquipSlot, ItemType, SkillTreeType, etc.)
│
├── Interfaces/
│   ├── ICombat.cs             ← IDamageable, IDamageDealer, ITargetLockable, IWeaponUser
│   ├── IWeapon.cs             ← IWeapon + WeaponAnimationSet + AnimationActionData
│   ├── IStatProvider.cs       ← IStatProvider, IStatModifiable + StatModifier
│   └── IAnimationController.cs ← IAnimationController + AnimationPriority/Phase
│
├── Weapons/
│   ├── WeaponHandler.cs       ← Weapon equip + stat modifiers + anim set
│   └── WeaponVisualHandler.cs ← Visual mount/sheath/draw
│
├── Input/
│   └── PlayerInputHandler.cs  ← Input buffering (0.15s) + skill input tracking
│
├── Camera/
│   └── CameraController.cs    ← Third-person camera
│
├── UI/
│   ├── HUDPanel.cs            ← HP/MP/Stamina/Chi/EXP bars
│   ├── InventoryPanel.cs      ← Grid inventory UI
│   ├── DamagePopup.cs         ← Floating damage numbers
│   └── DeathPanel.cs          ← Death screen + respawn
│
├── Editor/                    ← Editor-only tools (#if UNITY_EDITOR)
│   ├── RPGMegaSetup.cs        ← One-click setup (Player, Singletons, SpawnZone)
│   ├── RPGModularSetupWizard.cs ← Comprehensive setup window
│   └── RPGAnimationSetup.cs   ← Animator Controller builder
│
└── Testing/
    ├── DummyEnemy_VerticalSlice.cs ← 4-mode test enemy
    └── VerticalSliceSetup.cs       ← One-click test scene setup
```

---

## 3. Design Patterns Sử Dụng

### 3.1. Facade Pattern — `Game.cs`

Truy cập mọi system qua 1 static class duy nhất:

```csharp
// ĐÚNG — dùng facade
float hp = Game.Health.CurrentHP;
Game.Weapon.EquipWeapon(sword, WeaponSlot.MainHand);
Game.SkillBook.LearnOrUpgrade(skill);

// TRÁNH — find component trực tiếp
var stats = FindFirstObjectByType<CharacterStats>(); // chậm, fragile
```

### 3.2. Component Composition — `PlayerCore.cs`

Thay vì 1 class God Object, player là tập hợp nhiều MonoBehaviour nhỏ:

```
PlayerCore = Stats + Health + Combat + Skill + Inventory + ...
```

`PlayerCore.Awake()` tự `GetComponent<>()` tất cả. Không cần wire thủ công.

### 3.3. State Machine — Combat & Locomotion

```
CombatStateMachine
  ├── CombatIdleState
  ├── CombatEngagedState
  ├── AttackingState
  ├── DodgeState
  ├── HitStunState
  └── DeadState

LocomotionStateMachine
  ├── IdleState
  ├── MoveState
  ├── JumpState
  ├── FallState
  └── SprintState
```

### 3.4. Pipeline Pattern — `DamagePipeline.cs`

Damage đi qua chuỗi processors theo priority:

```
CritProcessor(10) → DodgeProcessor(20) → BlockProcessor(30) → DefenseProcessor(40) → MinDamageProcessor(100)
```

### 3.5. Observer Pattern — C# Events

```csharp
// Publisher
public event Action<DamageResult> OnDamageTaken;

// Subscriber
enemyBase.OnDamageTaken += HandleEnemyHit;
```

### 3.6. ScriptableObject Configuration

Tất cả data game (skills, weapons, items, quests) đều là ScriptableObject:
- Designer edit trong Inspector, không cần code
- Reference bằng drag-and-drop
- Runtime immutable (original data không bị thay đổi)

---

## 4. Cách Truy Cập Systems (Game Facade)

```csharp
// ═══ Player Systems ═══
Game.Player         // PlayerCore (hub)
Game.Stats          // CharacterStats
Game.Health         // HealthSystem
Game.Inv            // Inventory
Game.Equip          // EquipmentSystem
Game.Level          // LevelSystem
Game.Status         // StatusEffectSystem

// ═══ Combat Systems ═══
Game.Combat         // CombatStateMachine
Game.LockOn         // LockOnSystem
Game.Weapon         // WeaponHandler
Game.AutoAttack     // AutoAttackSystem
Game.Focus          // FocusGauge
Game.DamageHandler  // PlayerDamageHandler

// ═══ Skill Systems ═══
Game.SkillBook      // PlayerSkillBook
Game.SkillBar       // SkillBar (6 slots)
Game.Skill          // SkillCaster
Game.Combo          // ComboTracker

// ═══ Services (Singletons) ═══
Game.Loc            // LocalizationService
// DialogueSystem.Instance
// ShopService.Instance
// LootSystem.Instance
```

**Quan trọng:** Luôn null-check khi gọi systems vì có thể chưa init:

```csharp
Game.Level?.AddExp(100f);  // safe
Game.Inv?.AddGold(50);     // safe
```

---

## 5. Luồng Data: Từ Asset Đến Runtime

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│  ScriptableObject │     │   MonoBehaviour   │     │    Runtime State  │
│  (Immutable Data) │ ──→ │   (System Logic)  │ ──→ │   (Mutable State) │
└──────────────────┘     └──────────────────┘     └──────────────────┘

Ví dụ:
WeaponData (asset)  →  WeaponHandler (component)  →  currentWeapon, stat modifiers
SkillData (asset)   →  PlayerSkillBook (component) →  learnedSkills dict, skill levels
EnemyData (asset)   →  EnemyBase (component)       →  currentHP, combatState
QuestData (asset)   →  QuestTracker (component)     →  quest instances, progress[]
ItemData (asset)    →  Inventory (component)        →  ItemStack[] slots
```

**Quy tắc:** KHÔNG modify ScriptableObject at runtime. Chỉ đọc config từ SO, lưu state riêng.

---

## 6. Hệ Thống Combat — Luồng Damage

### Player → Enemy

```
1. Player nhấn LMB (hoặc auto-attack khi lock-on)
2. AutoAttackSystem.TryAutoAttack()
   → Lấy weapon animation: WeaponHandler.GetNormalAttackAction(comboIndex)
   → CombatStateMachine → AttackingState
   → AnimationController.PlayAction(actionData)

3. Animation đến phase Active:
   → HitboxManager kích hoạt hitbox collider
   → OnTriggerEnter detect enemy (IDamageable)

4. PlayerDamageHandler.CalculateDamage(isHeavy)
   → rawDamage = PhysicalAttack + weapon.BaseDamage
   → Tạo DamageInfo { RawDamage, Type, Source, etc. }

5. enemy.TakeDamage(damageInfo)
   → EnemyBase chạy damage pipeline thủ công:
     → Check dodge → Check block → Crit → Defense → Min damage
   → currentHP -= finalDamage
   → Play hit reaction animation
   → Nếu HP ≤ 0 → HandleDeath() → OnDeath event

6. LootSystem.ProcessEnemyDeath()
   → Grant EXP → Grant Gold → Roll loot → Add items
   → QuestTracker.ReportKill(enemyData)
```

### Enemy → Player

```
1. EnemyAI state = Attack
   → EnemyAI.PerformAttack() → Physics.OverlapSphere
   → Tìm PlayerDamageHandler

2. PlayerDamageHandler.TakeDamage(DamageInfo)
   → DamagePipeline.Process(damageInfo, context)
   → 5-stage chain: Crit → Dodge → Block → Defense → Min
   → HealthSystem.ApplyDamage(finalDamage)
   → currentHP -= damage
   → CombatStateMachine → HitStunState (0.4s normal, 0.6s heavy)
   → Nếu HP ≤ 0 → DeathSystem handles
```

---

## 7. Hệ Thống Stat & Modifier

### 7 Base Stats

| Stat | Ý nghĩa | Ảnh hưởng chính |
|------|---------|-----------------|
| STR | Strength | Physical ATK, Physical DEF |
| INT | Intelligence | Magic ATK, Magic DEF, Max MP |
| AGI | Agility | Attack Speed, Move Speed, Dodge |
| DEX | Dexterity | Crit Chance, Attack Speed |
| VIT | Vitality | Max HP, Stamina, Physical DEF |
| LUK | Luck | Crit Damage, Dodge, drop rate |
| TECH | Technique | Parry Window |

### Modifier System

3 loại modifier, áp dụng theo thứ tự:

```
1. Flat:        base + flat
2. PercentAdd:  (base + flat) × (1 + sum_of_percentAdd)
3. PercentMult: result × percentMult₁ × percentMult₂ × ...
```

**Ví dụ:** Base STR = 10, Sword +3 (Flat), Ring +10% (PercentAdd), Buff ×1.2 (PercentMult)
```
= (10 + 3) × (1 + 0.10) × 1.2
= 13 × 1.10 × 1.2
= 17.16
```

### Modifier Sources

```csharp
// Equipment
var mods = weapon.CreateEquipModifiers(); // StatModifier[]
Game.Stats.AddModifier(mod);

// Status Effect
// StatusEffectSystem tự apply mods khi effect active

// Passive Skill
// PlayerSkillBook tự apply khi learn/upgrade

// Pet Fusion
// TamerSystem tự apply khi fuse active
```

---

## 8. State Machines

### Combat State Machine

```
                    ┌────────────┐
                    │  CombatIdle │◄────────────────────────┐
                    └──────┬─────┘                          │
                           │ (lock-on / aggro)              │ (no target timeout)
                    ┌──────▼──────┐                         │
                    │  Engaged     │◄─────────┐             │
                    └──────┬──────┘           │             │
                           │                  │             │
              ┌────────────┼────────────┐     │             │
              │            │            │     │             │
       ┌──────▼───┐ ┌──────▼───┐ ┌─────▼──┐  │             │
       │ Attacking │ │  Dodge   │ │ Skill  │  │             │
       │ (combo)   │ │ (i-frame)│ │ Cast   │  │             │
       └──────┬───┘ └──────┬───┘ └────┬───┘  │             │
              │            │          │       │             │
              └────────────┼──────────┘       │             │
                           │                  │             │
                    ┌──────▼──────┐           │             │
                    │  HitStun    ├───────────┘             │
                    └──────┬──────┘                         │
                           │ (HP ≤ 0)                       │
                    ┌──────▼──────┐                         │
                    │    Dead      │                         │
                    └─────────────┘                         │
```

### Enemy AI State Machine

```
              ┌──────┐
              │ Idle  │◄─── (respawn / retreat arrive)
              └──┬───┘
                 │ (patrol timer)
              ┌──▼────┐
              │ Patrol │◄─── (arrived at patrol point)
              └──┬────┘
                 │ (detect player)
              ┌──▼────┐
              │ Alert  │ (face player, decide)
              └──┬────┘
                 │ (threat level ≥ Normal)
              ┌──▼────┐
              │ Chase  │ (NavMesh pursuit)
              └──┬────┘
                 │ (in attack range)          │ (leash exceeded)
              ┌──▼────┐                    ┌──▼─────┐
              │ Attack │                    │ Retreat │ (return to spawn)
              └──┬────┘                    └────────┘
                 │ (HP low)
              ┌──▼────┐
              │ Flee   │ (run 5s then retreat)
              └───────┘
```

---

## 9. Event System & Cách Lắng Nghe

### Pattern

```csharp
// Trong system A (publisher):
public event Action<float> OnExpGained;
// ...
OnExpGained?.Invoke(amount);

// Trong system B (subscriber):
void Start()
{
    Game.Level.OnLevelUp += HandleLevelUp;
}

void OnDestroy()
{
    if (Game.Level != null)
        Game.Level.OnLevelUp -= HandleLevelUp;
}

void HandleLevelUp(int newLevel)
{
    Debug.Log($"Level up! New level: {newLevel}");
}
```

### Key Events Reference

| System | Event | Parameters | Khi nào fire |
|--------|-------|------------|-------------|
| HealthSystem | OnResourceChanged | (ResourceType, current, max) | HP/MP/Stamina/Chi thay đổi |
| CharacterStats | OnStatChanged | (StatType, oldVal, newVal) | Bất kỳ stat nào thay đổi |
| LevelSystem | OnLevelUp | (int newLevel) | Level up |
| LevelSystem | OnExpGained | (float amount) | Nhận EXP |
| IDamageable | OnDamageTaken | (DamageResult) | Bị damage |
| IDamageable | OnDeath | () | Chết |
| IDamageDealer | OnDamageDealt | (IDamageable, DamageResult) | Gây damage |
| WeaponHandler | OnWeaponChanged | (IWeapon, WeaponSlot) | Đổi vũ khí |
| Inventory | OnItemAdded | (ItemData, int qty) | Thêm item |
| Inventory | OnGoldChanged | (int total) | Gold thay đổi |
| SkillBar | OnSkillBarChanged | (int slot, SkillData) | Skill bar thay đổi |
| SkillCaster | OnSkillCastStart | (SkillData) | Bắt đầu cast |
| SkillCaster | OnSkillCastComplete | (SkillData) | Cast xong |
| QuestTracker | OnQuestAccepted | (QuestData) | Nhận quest |
| QuestTracker | OnObjectiveProgress | (QuestData, int, int, int) | Tiến trình quest |
| QuestTracker | OnQuestCompleted | (QuestData) | Quest hoàn thành |
| DialogueSystem | OnDialogueStart | (DialogueData) | Bắt đầu hội thoại |
| DialogueSystem | OnNodeChanged | (DialogueNode) | Chuyển node |
| DialogueSystem | OnDialogueEnd | (DialogueData) | Kết thúc hội thoại |
| StatusEffectSystem | OnEffectApplied | (ActiveStatusEffect) | Buff/debuff applied |
| PackManager | OnThreatChanged | (ThreatLevel) | Threat thay đổi |
| TamerSystem | OnPetCaptured | (PetInstance) | Bắt pet thành công |

---

## 10. ScriptableObject Data Layer

### Cách Tạo Asset Mới

| Bạn muốn tạo | Menu Path | Class |
|--------------|-----------|-------|
| Vũ khí mới | Create > RPG > Weapon Data | WeaponData |
| Skill mới | Create > Game > Skill Data | SkillData |
| Item mới | Create > Game > Item Data | ItemData |
| Enemy type mới | Create > Game > Enemy Data | EnemyData |
| Quest mới | Create > Game > Quest Data | QuestData |
| NPC mới | Create > Game > NPC Data | NPCData |
| Dialogue mới | Create > Game > Dialogue Data | DialogueData |
| Shop mới | Create > Game > Shop Data | ShopData |
| Loot table | Create > Game > Loot Table | LootTable |
| Status effect | Create > Game > Status Effect Data | StatusEffectData |
| Skill tree | Create > Game > Skill Tree Data | SkillTreeData |
| Pet type | Create > Game > Pet Data | PetData |
| Recipe | Create > Game > Recipe Data | RecipeData |
| Zone | Create > Game > Zone Data | ZoneData |

### Database Assets (Registry)

2 database dùng để lookup nhanh:
- `SkillDatabase`: chứa `allSkills[]` + `allTrees[]`, method `GetSkillByID(string)`
- `ItemDatabase`: chứa `allItems[]` + `allWeapons[]`, method `GetItemByID(string)`

**Quan trọng:** Khi tạo skill/item mới, nhớ thêm vào database tương ứng.

---

## 11. Interface Map

### IDamageable (ai có thể bị đánh)

Implemented bởi: `PlayerDamageHandler`, `EnemyBase`

```csharp
interface IDamageable
{
    float CurrentHP { get; }
    float MaxHP { get; }
    bool IsAlive { get; }
    ECombatState CurrentCombatState { get; }
    DamageResult TakeDamage(DamageInfo damageInfo);
    event Action<DamageResult> OnDamageTaken;
    event Action OnDeath;
}
```

### IDamageDealer (ai có thể gây damage)

Implemented bởi: `PlayerDamageHandler`, `EnemyBase`

```csharp
interface IDamageDealer
{
    DamageInfo CalculateDamage(bool isHeavyAttack = false);
    event Action<IDamageable, DamageResult> OnDamageDealt;
}
```

### ITargetLockable (ai có thể bị lock-on)

Implemented bởi: `PlayerDamageHandler`, `EnemyBase`

```csharp
interface ITargetLockable
{
    Transform LockOnPoint { get; }
    bool CanBeLocked { get; }
}
```

### IWeapon (thông tin vũ khí)

Implemented bởi: `WeaponData`

```csharp
interface IWeapon
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
```

### IStatProvider (đọc stats)

Implemented bởi: `CharacterStats`

### IStatModifiable (đọc + sửa stats)

Extends `IStatProvider`, implemented bởi: `CharacterStats`

---

## 12. Enum Reference

### Combat

```csharp
ECombatState { Idle, Combat, Attacking, SkillCharge, SkillExecute, ComboReady, Dodge, HitStun, Knockback, Blocking, Dead }
DamageType { Slash, Pierce, Strike, Fire, Ice, Lightning, Dark, Holy }
PhysicalDamageGroup { Sharp, Slash, Ranged, Blunt }
WeaponType { Unarmed, Sword, GreatSword, Shield, Spear, Halberd, Bow, Bowgun, Staff, MagicDevice, Dagger, Knuckle, Katana, DualWield, Axe }
WeaponSlot { MainHand, OffHand }
```

### Skills

```csharp
SkillTreeType { Blade, GreatSword, Katana, DualSword, Guardian, Spear, Halberd, Archery, Martial, Tao, Sorcery, Blacksmith, Alchemist, Tamer, Survival }
SkillCategory { Active, Passive }
SkillTargetType { Self, SingleTarget, AoE_Circle, AoE_Cone, AoE_Line, Projectile, Party }
DamageScaleType { Physical, Magical }
```

### Items

```csharp
ItemType { Weapon, Armor, Consumable, Material, QuestItem, Accessory, CaptureItem, CraftingTool, PetFood, EnhancementStone }
ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
EquipSlot { Head, Body, Legs, Feet, MainHand, OffHand, Accessory1, Accessory2 }
```

### Stats

```csharp
StatType { STR, INT, AGI, DEX, VIT, LUK, TECH }
ModifierType { Flat, PercentAdd, PercentMult }
```

### AI & Enemy

```csharp
EnemyAIState { Idle, Patrol, Alert, Chase, Attack, Retreat, Flee, ReactiveDefend, Dead }
EnemyTier { Normal, Elite, MiniBoss, Boss }
ThreatLevel { Terrified, Wary, Normal, Aggressive, Bloodlust }
```

### Quest & NPC

```csharp
QuestType { Main, Side, Daily, Weekly }
QuestState { Available, Active, Completed, TurnedIn }
ObjectiveType { Kill, Collect, Talk, Reach, Craft, Capture }
NPCRole { Merchant, QuestGiver, Blacksmith, Alchemist, Trainer, PetTrainer }
DialogueNodeType { Text, Choice, Condition, Event }
```

### Other

```csharp
PetState { Idle, Following, Fighting, Stored }
PetRarity { Common, Uncommon, Rare, Epic, Legendary }
CraftType { Forge, Brew, Enhance }
EnhanceResult { Success, Fail, Downgrade }
ZoneType { Town, Field, Dungeon, Boss, Arena }
AnimationPriority { Locomotion(0), CombatIdle(10), NormalAttack(20), Skill(30), Block(35), HitReaction(40), Knockback(50), Stun(60), Death(100) }
AnimationPhase { Startup, Active, Recovery, Done }
```

---

## 13. Editor Tools

### Menu: RPG >

| Menu Item | Chức năng |
|-----------|-----------|
| `Mega Setup Player` | Gắn 22+ components lên selected GameObject |
| `Mega Setup Singletons` | Tạo [RPG_Singletons] với 9 service components |
| `Mega Setup SpawnZone` | Tạo PackManager + VAT_MobSpawner |
| `Setup Quest Tracker on Player` | Gắn QuestTracker lên Player |
| `Validate Player Setup` | Kiểm tra Player có đủ components |
| `Animation Setup Wizard` | Tạo Animator Controller + Blend Tree từ FBX |

### Menu: RPG > Testing >

| Menu Item | Chức năng |
|-----------|-----------|
| `Setup Vertical Slice Scene` | One-click tạo full test scene |

### Setup Wizard Window

`RPG > Setup Wizard` mở window EditorWindow với tabs:
- **Player**: Component config, CharacterController, hitbox, weapon bones
- **Enemy**: EnemyData assign, lock-on points
- **Camera**: Camera controller config
- **Layers**: Layer/tag setup
- **Validate**: Check missing components
- **QuickCreate**: Tạo nhanh Weapon/Item/Skill assets

---

## 14. Conventions & Quy Ước Code

### Naming

```
Folders:         PascalCase (Core, Combat, Skill)
Scripts:         PascalCase (PlayerController.cs, EnemyBase.cs)
ScriptableObject: PascalCase + "Data" suffix (WeaponData, SkillData)
Enums:           PascalCase (SkillCategory, DamageType)
Events:          "On" prefix (OnDamageTaken, OnLevelUp)
Interfaces:      "I" prefix (IDamageable, IWeapon)
Private fields:  camelCase (currentHP, attackCooldownTimer)
Public props:    PascalCase (CurrentHP, MaxHP, IsAlive)
```

### Namespace

Tất cả trong namespace `RPGModular`:

```csharp
namespace RPGModular
{
    public class MyNewSystem : MonoBehaviour { ... }
}
```

Testing code trong `RPGModular.Testing`, Editor trong `RPGModular.Editor`.

### Inspector Attributes

Project dùng `BillInspector` custom attribute package:

```csharp
[BillTitle("System Name")]          // Header
[BillBoxGroup("Group")]             // Group fields
[BillSlider(0f, 100f)]              // Slider
[BillRequired("Message")]           // Required field warning
[BillReadOnly]                      // Can't edit at runtime
[BillShowIf("fieldName", value)]    // Conditional show
[BillHideIf("fieldName")]           // Conditional hide
[BillEnumToggleButtons]             // Enum as buttons
[BillTableList]                     // Table view for arrays
[BillInlineEditor]                  // Inline SO editor
[BillPreviewField]                  // Sprite preview
[BillSuffix("s")]                   // Unit suffix (seconds)
```

### Event Pattern

```csharp
// Khai báo
public event Action<ParamType> OnSomethingHappened;

// Fire (null-safe)
OnSomethingHappened?.Invoke(param);

// Subscribe/Unsubscribe (luôn cleanup)
void OnEnable() => source.OnEvent += Handler;
void OnDisable() => source.OnEvent -= Handler;
```

---

## 15. Hướng Dẫn Thêm Feature Mới

### Thêm Weapon Type Mới

1. Thêm enum value vào `WeaponType` trong `IWeapon.cs`
2. Thêm default animation config vào `WeaponAnimationSet.CreateDefault()` switch case
3. Tạo `WeaponData` asset mới
4. (Optional) Thêm `SkillTreeType` nếu cần skill tree riêng

### Thêm Stat Mới

1. Thêm vào `StatType` enum trong `IStatProvider.cs`
2. Thêm `base{StatName}` field vào `CharacterStats`
3. Thêm derived stat formula vào `CharacterStats` getter
4. Thêm localization key vào `en.json` / `vi.json`

### Thêm Enemy Type Mới

1. Tạo `EnemyData` asset mới
2. (Optional) Subclass `EnemyBase` nếu cần behavior đặc biệt
3. Tạo prefab: Model + EnemyBase + EnemyAI + NavMeshAgent + Collider
4. Tạo `LootTable` asset cho enemy
5. Wire vào `VAT_MobSpawner`

### Thêm Status Effect Mới

1. Tạo `StatusEffectData` asset
2. Chọn stack behavior (Refresh / AddDuration / StackIntensity / StackSeparate)
3. Khai báo stat modifiers nếu là buff/debuff
4. Khai báo tick effect nếu là DoT/HoT
5. Wire vào SkillData.appliedEffect hoặc SkillData.selfBuff

### Thêm Quest Type Mới

1. (Optional) Thêm `ObjectiveType` enum nếu cần objective type mới
2. Tạo `QuestData` asset
3. Khai báo objectives
4. Wire vào NPCData.availableQuests
5. Ensure `QuestTracker.Report*()` được gọi đúng chỗ

### Thêm NPC System Mới (vd: Enchanter)

1. Thêm `NPCRole.Enchanter` vào `GameEnums.cs`
2. (Optional) Thêm field vào `NPCData` với `[BillShowIf("role", NPCRole.Enchanter)]`
3. Viết `EnchantmentSystem.cs` MonoBehaviour trong `Core/`
4. Thêm vào Singletons setup (`RPGMegaSetup.cs`)
5. Tạo `EnchantmentData.cs` ScriptableObject trong `Data/`
6. Wire UI

---

## 16. FAQ / Gotchas

### Q: Tại sao Player không nhận damage?

Checklist:
- Player có `PlayerDamageHandler` component?
- Player có `HealthSystem` component?
- Enemy collider có detect đúng layer?
- `PlayerDamageHandler.TakeDamage()` có được gọi? (check Debug log)

### Q: Tại sao skill không cast?

Checklist:
- Skill đã learn? (`Game.SkillBook.GetSkillLevel(skill) > 0`)
- Đã equip vào SkillBar? (`Game.SkillBar.GetSkill(slot) != null`)
- Đủ MP/Chi? (`Game.Health.CurrentMana >= cost`)
- Cooldown xong? (`Game.SkillBar.CanUseSkill(slot)`)
- Weapon requirement met? (`skill.requiredWeapons` chứa weapon hiện tại)

### Q: Tại sao enemy không chase player?

Checklist:
- `EnemyAI` component có enabled?
- `NavMeshAgent` component có enabled?
- NavMesh đã bake? (Window > AI > Navigation)
- `EnemyAI.SetTarget()` đã gọi? (PackManager tự gọi nếu có)
- Player trong `detectionRange`?

### Q: Tại sao stats không thay đổi sau khi equip?

Checklist:
- Item có `statBonuses[]` không trống?
- `EquipmentSystem.Equip()` gọi thành công? (check requirements)
- `CharacterStats` component tồn tại trên Player?

### Q: Game.Player returns null?

- `Game.cs` dùng `FindFirstObjectByType<PlayerCore>()` lần đầu
- Đảm bảo PlayerCore component tồn tại trên 1 GameObject trong scene
- Check script execution order nếu gọi quá sớm

### Q: Làm sao test nhanh mà không cần model/animation?

1. Menu: `RPG > Testing > Setup Vertical Slice Scene`
2. Dùng `DummyEnemy_VerticalSlice` với Capsule primitive
3. Standing Dummy mode cho infinite HP, test damage output
4. Debug logs hiện chi tiết mỗi hit

---

## 17. Bản Đồ Dependencies

```
PlayerCore ──→ CharacterStats (IStatProvider)
           ──→ HealthSystem
           ──→ PlayerInputHandler
           ──→ PlayerController ──→ WeaponVisualHandler
           ──→ CombatStateMachine ──→ CombatStates
           ──→ LocomotionStateMachine ──→ LocomotionStates
           ──→ LockOnSystem
           ──→ WeaponHandler ──→ WeaponData (IWeapon)
           ──→ AutoAttackSystem ──→ WeaponHandler, LockOnSystem
           ──→ PlayerDamageHandler ──→ DamagePipeline, HealthSystem
           ──→ FocusGauge
           ──→ Inventory
           ──→ EquipmentSystem ──→ Inventory, CharacterStats
           ──→ LevelSystem
           ──→ StatusEffectSystem ──→ CharacterStats
           ──→ PlayerSkillBook ──→ LevelSystem
           ──→ SkillBar
           ──→ SkillCaster ──→ SkillBar, HealthSystem, CombatStateMachine
           ──→ ComboTracker

EnemyBase ──→ EnemyData (ScriptableObject)
          ──→ AnimationController
          ──→ DamagePipeline

EnemyAI ──→ EnemyData
        ──→ NavMeshAgent

PackManager ──→ EnemyAI[] (registered enemies)
VAT_MobSpawner ──→ EnemyData, PackManager

DialogueSystem ──→ DialogueData → DialogueNode[]
ShopService ──→ ShopData → Inventory
QuestTracker ──→ QuestData → QuestObjective[]
LootSystem ──→ LootTable → Inventory, LevelSystem
TamerSystem ──→ PetData
CraftingSystem ──→ RecipeData → Inventory
```

---

## Kết Luận

RPG Modular được thiết kế để:
1. **Dễ mở rộng:** Thêm weapon/skill/enemy = tạo ScriptableObject + wire
2. **Dễ test:** Dummy enemies + one-click scene setup
3. **Dễ maintain:** Mỗi system độc lập, giao tiếp qua events
4. **Designer-friendly:** Data nằm trong Inspector, không cần code

Khi gặp khó khăn:
- Chạy `RPG > Validate Player Setup` để check thiếu components
- Bật `showDamageLog` trên DummyEnemy để xem damage flow
- Check events: subscribe vào `OnDamageTaken`, `OnSkillCastFailed` để debug
- Đọc source code: mỗi file có XML summary comments
