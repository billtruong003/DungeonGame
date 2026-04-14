# RPG Modular - Hướng Dẫn Setup Chi Tiết (Vertical Slice)

> Tài liệu này bao gồm: vật liệu cần chuẩn bị, danh sách skill, cách setup vũ khí, NPC, mob, quest, trainer, dialogue, và toàn bộ luồng gameplay từ A-Z.

---

## MỤC LỤC

1. [Vật Liệu Cần Chuẩn Bị](#1-vật-liệu-cần-chuẩn-bị)
2. [Hệ Thống Skill Hiện Có](#2-hệ-thống-skill-hiện-có)
3. [Setup Vũ Khí](#3-setup-vũ-khí)
4. [Setup Player](#4-setup-player)
5. [Setup Enemy / Mob](#5-setup-enemy--mob)
6. [Setup NPC](#6-setup-npc)
7. [Setup Trainer (Dạy Skill)](#7-setup-trainer-dạy-skill)
8. [Setup Dialogue (Hội Thoại)](#8-setup-dialogue-hội-thoại)
9. [Setup Quest (Nhiệm Vụ)](#9-setup-quest-nhiệm-vụ)
10. [Cầm Vũ Khí & Equipment](#10-cầm-vũ-khí--equipment)
11. [Unlock Skill & Gắn Vào Nút](#11-unlock-skill--gắn-vào-nút)
12. [Luồng Nhiệm Vụ Chính](#12-luồng-nhiệm-vụ-chính)
13. [Setup Scene Hoàn Chỉnh](#13-setup-scene-hoàn-chỉnh)
14. [Dummy Enemy Để Test](#14-dummy-enemy-để-test)
15. [Checklist Vertical Slice](#15-checklist-vertical-slice)

---

## 1. Vật Liệu Cần Chuẩn Bị

### 1.1. Asset 3D (Models)

| Loại | Mô tả | Format |
|------|--------|--------|
| Player Model | Humanoid character (FBX) | FBX, Rig = Humanoid |
| Weapon Models | Mỗi loại vũ khí 1 prefab | FBX/Prefab |
| Enemy Models | Mob models (hoặc dùng Capsule placeholder) | FBX/Prefab |
| NPC Models | NPC models (hoặc dùng Cylinder placeholder) | FBX/Prefab |

### 1.2. Animations

Hệ thống dùng **VAT (Vertex Animation Texture)** + **Animator Controller** với Blend Tree.

**Player Animations cần:**

| Category | Clips | Ghi chú |
|----------|-------|---------|
| Exploration | Idle, Walk, Run, Sprint, Jump, Land, Fall, Interact, Idle2, Idle3 | 10 clips |
| Combat Shared | CombatIdle, Dodge, HitLight, HitHeavy, Knockback, Death | 6 clips |
| Per Weapon | NormalAtk1-4, HeavyAtk, SkillCast, BlockIdle, BlockHit, BlockBreak, Equip, Unequip, ParrySuccess, SpecialAtk | 13 clips mỗi weapon type |

**Enemy Animations cần:**

| Clip | Dùng cho |
|------|----------|
| Idle | Đứng yên |
| Walk | Patrol/Chase |
| Attack1, Attack2 | Đòn đánh |
| Hit | Bị đánh |
| Death | Chết |

> **TIP:** Editor tool `RPG > Animation Setup Wizard` sẽ tự tạo Animator Controller + Blend Tree từ FBX clips.

### 1.3. ScriptableObject Assets (Data)

Tạo qua **Right-click > Create** trong Project window:

| Asset | Menu Path | Cần cho |
|-------|-----------|---------|
| WeaponData | `RPG > Weapon Data` | Mỗi vũ khí |
| ItemData | `Game > Item Data` | Mỗi item (potion, material, quest item) |
| SkillData | `Game > Skill Data` | Mỗi skill |
| EnemyData | `Game > Enemy Data` | Mỗi loại enemy |
| QuestData | `Game > Quest Data` | Mỗi quest |
| NPCData | `Game > NPC Data` | Mỗi NPC |
| DialogueData | `Game > Dialogue Data` | Mỗi đoạn hội thoại |
| ShopData | `Game > Shop Data` | Mỗi shop |
| LootTable | `Game > Loot Table` | Mỗi bảng drop |
| StatusEffectData | `Game > Status Effect Data` | Mỗi buff/debuff |
| SkillTreeData | `Game > Skill Tree Data` | Mỗi nhánh skill |
| SkillDatabase | `Game > Skill Database` | 1 cái duy nhất, chứa all skills |
| ItemDatabase | `Game > Item Database` | 1 cái duy nhất, chứa all items |

### 1.4. UI Assets

| Loại | Mô tả |
|------|--------|
| Skill Icons | Sprite 64x64 hoặc 128x128 cho mỗi skill |
| Item Icons | Sprite cho mỗi item |
| NPC Portraits | Sprite chân dung NPC |
| HUD Prefabs | HUDPanel, InventoryPanel, DamagePopup, DeathPanel |

### 1.5. Layers & Tags

| Tag/Layer | Dùng cho |
|-----------|----------|
| Tag: `Player` | Player GameObject |
| Tag: `Enemy` | Enemy GameObjects |
| Layer: `Enemy` | Enemy collider (cho LockOnSystem detect) |
| Layer: `PlayerHitbox` | Player attack hitbox |
| Layer: `EnemyHitbox` | Enemy attack hitbox |

---

## 2. Hệ Thống Skill Hiện Có

### 2.1. Skill Tree Types (15 nhánh)

**Weapon Trees (8):**

| Tree | Tên | Vũ khí yêu cầu | Đặc điểm |
|------|------|-----------------|-----------|
| `Blade` | Kiếm Pháp | Sword (1H) | Cân bằng damage/speed |
| `GreatSword` | Trọng Kiếm Đạo | GreatSword (2H) | Damage cao, chậm |
| `Katana` | Nhẫn Đạo | Katana | Focus Gauge mechanic, precision |
| `DualSword` | Song Kiếm Thuật | DualWield | 4-hit combo, tốc độ cao |
| `Guardian` | Thủ Thuật | Sword + Shield | Block/Parry chuyên |
| `Spear` | Thương Pháp | Spear | Tầm xa, poke |
| `Halberd` | Kích Pháp | Halberd | AoE sweep |
| `Archery` | Xạ Thuật | Bow | Ranged, projectile |

**Shared Trees (3):**

| Tree | Tên | Đặc điểm |
|------|------|-----------|
| `Martial` | Võ Thuật | Knuckle/Barehand, dùng Stamina |
| `Tao` | Đạo Thuật | Dùng Chi, buff/debuff |
| `Sorcery` | Ma Thuật | Staff/MagicDevice, spell damage |

**Life Trees (3):**

| Tree | Tên | Đặc điểm |
|------|------|-----------|
| `Blacksmith` | Luyện Khí Sư | Craft vũ khí, enhance |
| `Alchemist` | Điều Chế Sư | Craft potion, brew |
| `Tamer` | Ngự Thú Sư | Bắt pet, fuse |

**Universal (1):**

| Tree | Tên | Đặc điểm |
|------|------|-----------|
| `Survival` | Sinh Tồn | HP regen, dodge, passive survivability |

### 2.2. Skill Structure

Mỗi skill gồm:

```
SkillData (ScriptableObject)
├── Identity: skillID, name, description, icon, treeType
├── Type: category (Active/Passive), targetType, scaleType
├── Cost: MP, Chi, castTime, cooldown
├── Damage: basePower, powerPerLevel, hitCount, scalingStat, scalingRatio
├── AoE: radius, coneAngle, range
├── Effects: appliedEffect, effectChance, selfBuff
├── Passive: passiveBonuses[] (stat modifiers)
├── Tree: tier (1-5), prerequisites[], maxLevel (10), spCostPerLevel[]
├── Animation: vatAnimClip, duration, comboWindow
└── Special: isBlockSkill, isParrySkill, superArmor
```

### 2.3. Default Skills (Built-in)

Hệ thống có sẵn 2 default skills trên SkillBar:
- **Block** (Slot 4): Giảm 40% damage, tốn Stamina, duration 1.5s
- **Parry** (Slot 5): Parry window 0.3s, stagger enemy 1s nếu thành công

### 2.4. Skill Damage Formula

```
power = basePower + powerPerLevel * (level - 1)
statDmg = (Physical ? PhysicalAttack : MagicAttack)
scaling = GetStat(primaryScalingStat) * scalingRatio
comboBonus = ComboTracker.GetComboDamageBonus()   // 1.0x → 1.5x
focusBonus = FocusGauge.GetDamageBonus()           // 1.0x → 1.5x (Katana only)
rawDamage = (statDmg + weaponATK + scaling) * (power/100) * comboBonus * focusBonus
```

### 2.5. Skill Point Economy

- Nhận **1 SP/level** (configurable trong LevelSystem)
- Tier 1-3: tốn 1-2 SP/level
- Tier 4-5: tốn 2-3 SP/level
- Default SP cost array: `[1, 1, 1, 2, 2, 2, 3, 3, 3, 3]`
- Reset skill: `PlayerSkillBook.ResetAllSkills(goldCost)` → refund toàn bộ SP

---

## 3. Setup Vũ Khí

### 3.1. Tạo WeaponData Asset

1. Project window > Right-click > `Create > RPG > Weapon Data`
2. Đặt tên: `Weapon_IronSword`, `Weapon_Katana`, v.v.
3. Điền thông tin trong Inspector:

```
Basic Info:
  - weaponName: "Iron Sword"
  - type: Sword (chọn từ 15 loại)
  - slot: MainHand hoặc OffHand
  - icon: kéo sprite vào

Visual:
  - weaponPrefab: kéo prefab 3D model vũ khí vào

Damage:
  - primaryDamageType: Slash/Pierce/Strike/Fire/Ice/Lightning/Dark/Holy
  - damageGroup: Sharp/Slash/Ranged/Blunt
  - baseDamage: 10-500
  - attackRange: 0.5-10m
  - attackSpeedModifier: 0.5-3.0 (1.0 = bình thường)

Stat Requirements (optional):
  - VD: STR >= 10, DEX >= 5

Stat Bonuses (optional):
  - VD: STR +3 (Flat), CritChance +5% (PercentAdd)

Animation:
  - useDefaultAnimSet: true (hệ thống tự gen combo theo WeaponType)
  - hoặc tắt và kéo customAnimationSet vào
```

### 3.2. Weapon Types & Combo Length

| WeaponType | Max Combo | Startup | Active | Đặc điểm |
|------------|-----------|---------|--------|-----------|
| Unarmed | 3 | 0.12s | 0.45s | Không cần vũ khí |
| Sword | 3 | 0.15s | 0.50s | Cân bằng |
| GreatSword | 3 | 0.25s | 0.60s | Chậm, mạnh |
| Katana | 3 | 0.18s | 0.48s | Focus Gauge |
| Dagger | 4 | 0.10s | 0.40s | Nhanh, 4 combo |
| DualWield | 4 | 0.12s | 0.45s | 4 combo |
| Shield | 2 | 0.20s | 0.50s | Block chuyên |
| Spear | 3 | 0.20s | 0.55s | Tầm xa |
| Halberd | 3 | 0.25s | 0.60s | AoE |
| Bow | 2 | 0.30s | 0.40s | Ranged |
| Bowgun | 2 | 0.15s | 0.35s | Ranged, nhanh hơn Bow |
| Staff | 2 | 0.20s | 0.50s | Magic |
| MagicDevice | 2 | 0.15s | 0.45s | Magic, nhẹ |
| Knuckle | 4 | 0.08s | 0.35s | Nhanh nhất |
| Axe | 3 | 0.22s | 0.55s | Heavy damage |

### 3.3. Setup Visual Mount Points

Trên Player hierarchy:

```
Player (root)
├── Model (child có Animator + AnimationController)
│   ├── Armature
│   │   ├── ... bones ...
│   │   ├── RightHand ← MainHandSlot (mount point cầm tay)
│   │   ├── LeftHand  ← OffHandSlot
│   │   ├── Spine     ← MainHandSheath (mount point đeo lưng)
│   │   └── Hip       ← OffHandSheath
```

**Trong WeaponHandler Inspector:**
- `mainHandSlot`: Kéo bone RightHand vào
- `offHandSlot`: Kéo bone LeftHand vào
- `mainHandSheath`: Kéo bone Spine/Back vào
- `offHandSheath`: Kéo bone Hip vào
- `startingMainHand`: Kéo WeaponData default vào

### 3.4. Dual-Wield Combinations

WeaponHandler tự detect và switch animation set:

| Main Hand | Off Hand | Animation Set |
|-----------|----------|---------------|
| Sword | Shield | Guardian (block animations override) |
| Sword | Dagger | DualWield combo |
| Sword | Sword | DualWield combo |
| Katana | (trống) | Katana + Focus Gauge active |
| GreatSword | (blocked) | 2H only, không dùng OffHand |

---

## 4. Setup Player

### 4.1. One-Click Setup

1. Tạo empty GameObject, đặt tên "Player"
2. Kéo 3D model FBX làm child → rename thành "Model"
3. Menu: `RPG > Mega Setup Player`

Tự động gắn **22+ components**:

```
Player (root) — tag: Player
├── PlayerCore          ← Hub truy cập mọi system
├── CharacterStats      ← 7 stats + derived stats
├── HealthSystem        ← HP/MP/Stamina/Chi
├── PlayerInputHandler  ← Input buffering
├── PlayerController    ← Mode switching (Explore ↔ Combat)
├── CombatStateMachine  ← Combat states
├── PlayerDamageHandler ← IDamageable + IDamageDealer
├── CombatLocomotion    ← Lock-on strafing
├── LockOnSystem        ← Target management
├── AutoAttackSystem    ← Auto combo on lock-on
├── FocusGauge          ← Katana mechanic
├── WeaponHandler       ← Weapon equip/unequip
├── WeaponVisualHandler ← Visual mount/sheath
├── LocomotionStateMachine ← Movement states
├── Inventory           ← Item storage (30 slots)
├── EquipmentSystem     ← 8-slot equipment
├── LevelSystem         ← EXP/Level (max 50)
├── StatusEffectSystem  ← Buff/debuff management
├── PlayerSkillBook     ← Skill learning
├── SkillBar            ← 6 skill slots
├── SkillCaster         ← Skill execution
├── ComboTracker        ← Combo damage bonus
├── CharacterController ← Unity physics
│
├── [Model] (child)
│   ├── Animator
│   └── AnimationController
│
├── [Hitboxes] (child)
│   └── HitboxManager
│
└── [LockOnPoint] (child) — position: (0, 1.2, 0)
```

### 4.2. Singletons Setup

Menu: `RPG > Mega Setup Singletons`

Tạo `[RPG_Singletons]` GameObject với:

```
[RPG_Singletons]
├── LootSystem          ← Loot drop + reward
├── DeathSystem         ← Death handling
├── DialogueSystem      ← Dialogue management
├── ShopService         ← Buy/sell
├── ZoneSystem          ← Zone transitions
├── SaveLoadSystem      ← Save/Load game
├── CraftingSystem      ← Crafting recipes
├── WeaponEnhancement   ← Weapon upgrade
└── TamerSystem         ← Pet capture/summon
```

### 4.3. Base Stats Mặc Định

| Stat | Base | Derived |
|------|------|---------|
| STR | 10 | PhysicalATK = STR×2 + DEX×0.5 |
| INT | 10 | MagicATK = INT×2.5 |
| AGI | 10 | AttackSpeed, MoveSpeed, DodgeChance |
| DEX | 10 | CritChance, AttackSpeed |
| VIT | 10 | MaxHP = base + VIT×15, PhyDEF = VIT×1.5 |
| LUK | 10 | CritDamage = 1.5 + LUK×0.015 |
| TECH | 10 | ParryWindow = 0.15 + TECH×0.005 |

---

## 5. Setup Enemy / Mob

### 5.1. Tạo EnemyData Asset

1. Right-click > `Create > Game > Enemy Data`
2. Điền:

```
Identity:
  enemyID: "slime_green"
  nameKey: "enemy.slime_green.name"   (key localization)
  icon: kéo sprite
  tier: Normal / Elite / MiniBoss / Boss

Stats:
  baseLevel: 1
  baseHP: 100
  baseDamage: 10
  moveSpeed: 3.5
  physicalDefense: 5
  magicDefense: 3
  damageType: Strike

Combat Behavior:
  attackRange: 2.0
  attackCooldown: 2.0
  detectionRange: 12
  dodgeChance: 0.05    (5%)
  blockChance: 0        (0%)

Rewards:
  expReward: 50
  goldReward: 10
  lootTable: kéo LootTable asset vào
```

### 5.2. Tạo Enemy GameObject

```
EnemyGameObject
├── EnemyBase           ← Gắn script, kéo EnemyData vào
├── EnemyAI             ← Auto-detect hoặc tự gắn
├── NavMeshAgent        ← RequireComponent of EnemyAI
├── CapsuleCollider     ← Trigger cho hitbox
├── [LockOnPoint] (child) — position (0, 1.2, 0)
│
└── [Model] (child, optional)
    ├── Animator
    └── AnimationController
```

**Hoặc dùng Menu:** `RPG > Mega Setup SpawnZone` → tạo PackManager + VAT_MobSpawner

### 5.3. Spawn Zone Setup

```
SpawnZone_Forest
├── PackManager         ← Quản lý threat level, giới hạn chasers
│   - baseChasers: 3    ← Max 3 con chase cùng lúc
│   - chaserRefillDelay: 1.0s
│
└── VAT_MobSpawner      ← Spawn enemies
    - enemyPrefab: kéo enemy prefab
    - enemyData: kéo EnemyData
    - packSize: 5        ← 5 con per pack
    - activationRange: 30
    - despawnRange: 50
    - respawnDelay: 30s
    - spawnRadius: 8
```

### 5.4. Threat Level System

PackManager tính threat dựa trên level gap:

| Level Gap | Threat |
|-----------|--------|
| Player >> Enemy | Terrified → mob chạy |
| Player > Enemy | Wary → mob cảnh giác |
| Bằng nhau | Normal |
| Player < Enemy | Aggressive |
| Player << Enemy | Bloodlust → mob attack ngay |

---

## 6. Setup NPC

### 6.1. Tạo NPCData Asset

1. Right-click > `Create > Game > NPC Data`
2. Điền theo role:

```
Identity:
  npcID: "npc_village_elder"
  nameKey: "npc.elder.name"
  portrait: kéo sprite chân dung

Interaction:
  role: QuestGiver / Merchant / Trainer / Blacksmith / Alchemist / PetTrainer

  Nếu Merchant:
    shopData: kéo ShopData asset

  Nếu QuestGiver:
    availableQuests: kéo QuestData assets

Dialogue:
  greetingDialogue: kéo DialogueData asset (câu mở đầu)
```

### 6.2. NPC Roles & Chức Năng

| Role | Chức năng | System liên quan |
|------|-----------|------------------|
| Merchant | Mua/bán item | ShopService |
| QuestGiver | Giao quest, nhận quest | QuestTracker |
| Blacksmith | Craft vũ khí, enhance | CraftingSystem, WeaponEnhancement |
| Alchemist | Craft potion | CraftingSystem |
| Trainer | Dạy skill mới | PlayerSkillBook |
| PetTrainer | Quản lý pet, hướng dẫn capture | TamerSystem |

### 6.3. Setup NPC trong Scene

```
NPC_Elder (GameObject)
├── CapsuleCollider (isTrigger = true, radius = 3) ← Vùng tương tác
├── NPC Script (custom, xử lý Interact) ← Bạn cần viết script này
│   - npcData: kéo NPCData asset
│   - Khi player nhấn F trong trigger → gọi DialogueSystem.StartDialogue()
│
└── [Model] (child)
    └── Animator
```

**Luồng tương tác NPC:**
```
Player đến gần → OnTriggerEnter → Hiện prompt "Nhấn F"
  → Player nhấn F (PlayerInputHandler.InteractInput)
    → NPC script đọc npcData.greetingDialogue
      → DialogueSystem.StartDialogue(greetingDialogue)
        → UI hiển thị hội thoại
          → Nếu là QuestGiver: dialogue event → AcceptQuest
          → Nếu là Merchant: dialogue event → OpenShop
          → Nếu là Trainer: dialogue event → Mở skill tree UI
```

---

## 7. Setup Trainer (Dạy Skill)

### 7.1. Tạo Trainer NPC

1. Tạo NPCData với `role = Trainer`
2. Tạo DialogueData cho trainer (xem Section 8)
3. Trong dialogue, dùng **Event Node** để mở skill tree:

```
DialogueNode:
  type: Event
  eventName: "open_skill_tree"
  eventParam: "Blade"          ← SkillTreeType name
  afterEventNodeID: 3          ← Node tiếp theo
```

### 7.2. Luồng Học Skill

```
1. Player nói chuyện với Trainer NPC
2. Dialogue Event "open_skill_tree" triggered
3. UI mở SkillTreePanel (bạn cần viết UI này)
4. UI hiển thị skills trong tree (từ SkillTreeData)
5. Player chọn skill → kiểm tra:
   - PlayerSkillBook.CanLearn(skillData)
     - Đủ SP? (LevelSystem.AvailableSkillPoints > 0)
     - Chưa max level? (currentLevel < maxLevel)
     - Đã học prerequisites? (tất cả prerequisite skills đạt required level)
6. Nếu OK → PlayerSkillBook.LearnOrUpgrade(skillData)
   - Trừ SP
   - Nếu Passive: apply stat modifiers ngay
   - Fire event OnSkillLearned
7. Nếu Active skill → player gắn vào SkillBar
```

### 7.3. Code Ví Dụ — Trainer Interaction

```csharp
// Trong NPC interaction script
public class TrainerNPC : MonoBehaviour
{
    [SerializeField] private NPCData npcData;
    [SerializeField] private SkillTreeData teachableTree;

    public void OnInteract()
    {
        // Mở dialogue trước
        DialogueSystem.Instance.StartDialogue(npcData.greetingDialogue);

        // Hoặc mở skill tree trực tiếp
        // OpenSkillTreeUI(teachableTree);
    }

    // Gọi từ DialogueSystem event
    public void OpenSkillTreeUI(SkillTreeData tree)
    {
        // UI logic: hiển thị skills trong tree
        foreach (var skill in tree.skills)
        {
            int currentLevel = Game.SkillBook.GetSkillLevel(skill);
            bool canLearn = Game.SkillBook.CanLearn(skill);
            // Render skill icon, level, SP cost, lock/unlock state
        }
    }

    // Gọi khi player nhấn "Học"
    public void LearnSkill(SkillData skill)
    {
        if (Game.SkillBook.CanLearn(skill))
        {
            Game.SkillBook.LearnOrUpgrade(skill);
            Debug.Log($"Đã học {skill.nameKey} level {Game.SkillBook.GetSkillLevel(skill)}");
        }
    }
}
```

---

## 8. Setup Dialogue (Hội Thoại)

### 8.1. Tạo DialogueData Asset

1. Right-click > `Create > Game > Dialogue Data`
2. Điền `dialogueID`: "dlg_elder_greeting"
3. Tạo nodes array:

### 8.2. Các Loại Node

**Text Node — NPC nói:**
```
nodeID: 0
type: Text
speakerNameKey: "npc.elder.name"
speakerPortrait: (sprite)
textKey: "dlg.elder.greeting.01"    ← key localization
nextNodeID: 1                       ← node tiếp (-1 = kết thúc)
```

**Choice Node — Player chọn:**
```
nodeID: 1
type: Choice
choices:
  [0] textKey: "dlg.choice.accept_quest"   targetNodeID: 2
  [1] textKey: "dlg.choice.ask_about_town"  targetNodeID: 5
  [2] textKey: "dlg.choice.goodbye"         targetNodeID: -1 (end)
```

**Condition Node — Rẽ nhánh theo điều kiện:**
```
nodeID: 3
type: Condition
conditionField: "quest_kill_slime_completed"   ← tên condition check
trueNodeID: 4     ← nếu đã hoàn thành quest
falseNodeID: 6    ← nếu chưa
```

**Event Node — Trigger game event:**
```
nodeID: 2
type: Event
eventName: "accept_quest"
eventParam: "quest_kill_slime"     ← questID
afterEventNodeID: 3                ← node tiếp theo
```

### 8.3. Ví Dụ Dialogue Hoàn Chỉnh — NPC Quest Giver

```
Node 0 (Text): "Chào ngươi, lữ khách! Làng ta đang bị slime quấy nhiễu..."
  → nextNodeID: 1

Node 1 (Choice): Player chọn:
  [0] "Để ta giúp!" → Node 2
  [1] "Có gì hay ho quanh đây?" → Node 5
  [2] "Tạm biệt" → -1

Node 2 (Event): accept_quest / "quest_kill_slime"
  → afterEventNodeID: 3

Node 3 (Text): "Cảm ơn! Hãy tiêu diệt 5 con slime trong khu rừng phía Bắc."
  → nextNodeID: -1

Node 5 (Text): "Có lò rèn của thợ Trần ở phía Đông, và quán thuốc của sư phụ Lê phía Tây."
  → nextNodeID: -1
```

### 8.4. Dialogue Events Phổ Biến

| eventName | eventParam | Chức năng |
|-----------|------------|-----------|
| `accept_quest` | questID | QuestTracker.AcceptQuest() |
| `complete_quest` | questID | QuestTracker.TurnIn() |
| `open_shop` | shopID | ShopService.OpenShop() |
| `open_skill_tree` | treeType | Mở skill tree UI |
| `give_item` | itemID:qty | Inventory.AddItem() |
| `give_exp` | amount | LevelSystem.AddExp() |
| `give_gold` | amount | Inventory.AddGold() |
| `teleport` | zoneID:spawnID | ZoneSystem.LoadZone() |

---

## 9. Setup Quest (Nhiệm Vụ)

### 9.1. Tạo QuestData Asset

1. Right-click > `Create > Game > Quest Data`
2. Điền:

```
Identity:
  questID: "quest_kill_slime"
  nameKey: "quest.kill_slime.name"
  descKey: "quest.kill_slime.desc"
  questType: Main / Side / Daily / Weekly
  icon: (sprite)

Requirements:
  requiredLevel: 1
  prerequisiteQuests: []    ← quests cần hoàn thành trước

Objectives:
  [0] type: Kill
      targetEnemy: (kéo EnemyData "slime_green")
      requiredCount: 5
      descKey: "quest.kill_slime.obj1"

  [1] type: Collect
      targetItem: (kéo ItemData "slime_gel")
      requiredCount: 3
      descKey: "quest.kill_slime.obj2"

Rewards:
  expReward: 200
  goldReward: 100
  spReward: 1
  itemRewards:
    [0] item: (kéo ItemData "health_potion"), quantity: 5
```

### 9.2. Quest States Flow

```
Available → (AcceptQuest) → Active → (Complete all objectives) → Completed → (TurnIn) → TurnedIn
     ↑                         |
     └──── (AbandonQuest) ─────┘
```

### 9.3. Objective Types

| Type | Cách track | Trigger |
|------|-----------|---------|
| Kill | ReportKill(EnemyData) | Khi enemy chết, so sánh EnemyData |
| Collect | ReportCollect(ItemData) | Khi nhặt item, so sánh ItemData |
| Talk | ReportProgress(Talk, npcID) | Khi nói chuyện NPC |
| Reach | ReportProgress(Reach, zoneID) | Khi vào zone |
| Craft | ReportProgress(Craft, recipeID) | Khi craft xong |
| Capture | ReportProgress(Capture, petID) | Khi bắt pet |

### 9.4. Code Track Quest Progress

```csharp
// Khi enemy chết (trong LootSystem hoặc EnemyBase.HandleDeath)
var questTracker = Game.Player?.GetComponent<QuestTracker>();
questTracker?.ReportKill(enemyData);

// Khi nhặt item
questTracker?.ReportCollect(itemData);

// Khi nói chuyện NPC
questTracker?.ReportProgress(ObjectiveType.Talk, npcData.npcID);

// Khi quest hoàn thành, player quay về NPC turn in
questTracker?.TurnIn(questData);
// → tự động grant: EXP, Gold, Items
```

---

## 10. Cầm Vũ Khí & Equipment

### 10.1. Equip Vũ Khí (Code)

```csharp
// Equip weapon thông qua WeaponHandler
WeaponData sword = /* load hoặc reference */;
Game.Weapon.EquipWeapon(sword, WeaponSlot.MainHand);

// Equip off-hand
WeaponData shield = /* reference */;
Game.Weapon.EquipWeapon(shield, WeaponSlot.OffHand);

// Unequip
Game.Weapon.UnequipWeapon(WeaponSlot.MainHand);
```

### 10.2. Equip Armor/Accessory (Code)

```csharp
// Equip thông qua EquipmentSystem
ItemData helmet = /* reference */;
Game.Equip.Equip(helmet, EquipSlot.Head);

// 8 slots:
// Head, Body, Legs, Feet, MainHand, OffHand, Accessory1, Accessory2

// Unequip → item trả về Inventory
Game.Equip.Unequip(EquipSlot.Head);
```

### 10.3. Equipment Flow

```
Player mở Inventory UI
  → Chọn item có isEquippable = true
    → Kiểm tra requirements (stat min)
      → EquipmentSystem.Equip(item, slot)
        → Unequip item cũ (nếu có) → trả về Inventory
        → Remove item mới khỏi Inventory
        → Apply StatModifiers (STR+3, etc.)
        → CharacterStats recalculate
        → Nếu Weapon: WeaponHandler cũng equip visual
        → Fire OnEquipped event
```

### 10.4. Sheath / Unsheath

```
Exploration Mode:
  → WeaponVisualHandler: vũ khí ở sheath position (lưng/hông)

Vào Combat (aggro/lock-on):
  → PlayerController.OnModeChanged → Combat
    → WeaponVisualHandler.UnsheathWeapons()
      → Move prefab từ sheath mount → hand mount
      → Play Equip animation

Thoát Combat (5s không enemy):
  → PlayerController.OnModeChanged → Exploration
    → WeaponVisualHandler.SheathWeapons()
      → Move prefab từ hand mount → sheath mount
      → Play Unequip animation
```

---

## 11. Unlock Skill & Gắn Vào Nút

### 11.1. Unlock / Learn Skill

```csharp
// Kiểm tra có thể học
SkillData fireball = /* reference */;
bool canLearn = Game.SkillBook.CanLearn(fireball);
// Checks: đủ SP, chưa max, đã học prerequisites

// Học skill
if (canLearn)
{
    Game.SkillBook.LearnOrUpgrade(fireball);
    // → Trừ SP
    // → Nếu Passive: apply stat modifier ngay
    // → Fire OnSkillLearned(fireball, newLevel)
}

// Check level hiện tại
int level = Game.SkillBook.GetSkillLevel(fireball); // 0 = chưa học
```

### 11.2. Gắn Skill Vào Nút (Skill Bar)

```csharp
// SkillBar có 6 slots:
// Slot 0-3: Active skills (key 1-2-3-4)
// Slot 4: Block (default)
// Slot 5: Parry (default)

// Gắn skill vào slot 0 (key 1)
Game.SkillBar.EquipSkill(fireball, 0);

// Gắn skill vào slot 1 (key 2)
SkillData heal = /* reference */;
Game.SkillBar.EquipSkill(heal, 1);

// Lấy skill ở slot
SkillData equipped = Game.SkillBar.GetSkill(0); // fireball

// Check cooldown
bool canUse = Game.SkillBar.CanUseSkill(0);
float cdRemaining = Game.SkillBar.GetCooldownRemaining(0);
```

### 11.3. Sử Dụng Skill (Input → Execution)

```
Player nhấn key 1 (skill slot 0)
  → PlayerInputHandler records SkillInput[0] = true
    → SkillCaster.Cast(0)
      → Check: skill learned? (level > 0)
      → Check: SkillBar.CanUseSkill(0)? (cooldown, weapon req)
      → Check: có đủ MP? Chi?
        → HealthSystem.TryConsumeMana(cost)
        → HealthSystem.TryConsumeChi(chiCost)
      → SkillBar.StartCooldown(0, skill.cooldown)
      → Nếu castTime > 0:
        → CombatStateMachine → SkillChargeState (đứng cast)
        → Sau castTime → SkillExecuteState
      → Nếu castTime = 0:
        → CombatStateMachine → SkillExecuteState trực tiếp
      → SkillExecuteState:
        → Play animation
        → Activate hitbox (AoE check)
        → Calculate damage + apply
        → Apply effects (buff/debuff)
        → Fire OnSkillCastComplete
```

### 11.4. Ví Dụ Setup Skill Bar Cho Vertical Slice

```csharp
// Trong Start() hoặc sau khi player load save
void SetupDefaultSkills()
{
    // Giả sử đã tạo SkillData assets
    var slash = Resources.Load<SkillData>("Skills/Blade_Slash");
    var heal = Resources.Load<SkillData>("Skills/Tao_Heal");
    var aoe = Resources.Load<SkillData>("Skills/Sorcery_Fireball");

    // Học skill (cần đủ SP)
    Game.SkillBook.LearnOrUpgrade(slash);
    Game.SkillBook.LearnOrUpgrade(heal);
    Game.SkillBook.LearnOrUpgrade(aoe);

    // Gắn vào bar
    Game.SkillBar.EquipSkill(slash, 0);  // Key 1
    Game.SkillBar.EquipSkill(heal, 1);   // Key 2
    Game.SkillBar.EquipSkill(aoe, 2);    // Key 3
    // Slot 3 (Key 4): trống hoặc custom
    // Slot 4: Block (default)
    // Slot 5: Parry (default)
}
```

---

## 12. Luồng Nhiệm Vụ Chính

### 12.1. Main Quest Flow (Ví dụ Vertical Slice)

```
┌─────────────────────────────────────────────────────────┐
│  PROLOGUE: Đến Làng                                     │
│  Player spawn → đi đến NPC_Elder                        │
│  → Dialogue: "Chào mừng đến Bình An thôn..."           │
│  → Choice: nhận quest "Mối họa Slime"                   │
└─────────────────────┬───────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────┐
│  QUEST 1: Mối Họa Slime (Kill quest)                   │
│  Objective: Tiêu diệt 5 Slime                           │
│  → Player đi đến SpawnZone_Forest                       │
│  → Đánh 5 con Slime (auto-track via QuestTracker)       │
│  → Nhặt loot (Slime Gel, Gold, EXP)                     │
│  → Quest complete notification                           │
└─────────────────────┬───────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────┐
│  TURN IN: Quay về NPC_Elder                             │
│  → Dialogue: Condition check quest_completed             │
│  → "Tốt lắm! Đây là phần thưởng."                      │
│  → Event: complete_quest → grant rewards                 │
│  → Nhận 200 EXP, 100 Gold, 5 Health Potion              │
│  → LEVEL UP! → nhận 5 Stat Points + 1 SP                │
└─────────────────────┬───────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────┐
│  QUEST 2: Tìm Thầy Kiếm (Talk quest)                   │
│  Prerequisite: quest_kill_slime completed                │
│  Objective: Nói chuyện với NPC_SwordTrainer              │
│  → Player đi đến Trainer                                 │
│  → Dialogue: "Ta sẽ dạy ngươi kiếm thuật..."           │
│  → Event: open_skill_tree / "Blade"                      │
│  → Player học Blade_Slash (skill đầu tiên, free)         │
│  → Gắn vào Skill Bar slot 1                             │
│  → Quest complete                                        │
└─────────────────────┬───────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────┐
│  QUEST 3: Thử Sức (Kill quest + Collect)                │
│  Objective 1: Tiêu diệt 3 Elite Slime (dùng skill mới) │
│  Objective 2: Thu thập 3 Slime Core                      │
│  → Chiến đấu dùng combo + skill                         │
│  → Quest complete → Nhận vũ khí mới (Iron Sword)        │
└─────────────────────┬───────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────┐
│  QUEST 4: Boss Battle (Main Quest Climax)               │
│  Prerequisite: quest_3 completed                         │
│  Objective: Tiêu diệt King Slime (Boss tier)            │
│  → Boss zone transition                                  │
│  → Boss fight: HP 5000, patterns                         │
│  → Victory → Quest complete                              │
│  → BIG rewards → Vertical Slice END                      │
└─────────────────────────────────────────────────────────┘
```

### 12.2. Luồng Code Chi Tiết

```
[Scene Load]
  → Game.cs tìm PlayerCore (FindFirstObjectByType)
  → PlayerCore.Awake() auto-find tất cả components
  → Singletons Awake() → register instances

[Player Đến Gần NPC]
  → OnTriggerEnter (NPC collider)
  → UI hiện "Press F to interact"

[Nhấn F]
  → PlayerInputHandler.InteractInput = true
  → NPC script detect → gọi DialogueSystem.StartDialogue(npcData.greetingDialogue)
  → DialogueSystem.OnDialogueStart event → UI mở dialogue panel

[Dialogue Chạy]
  → Text node → UI hiển thị text (Loc.Get(textKey))
  → Player nhấn → DialogueSystem.Advance() → next node
  → Choice node → UI hiển thị choices → player click
    → DialogueSystem.SelectChoice(idx) → jump to target node
  → Condition node → evaluate → branch
  → Event node → trigger game event

[Accept Quest Event]
  → eventName = "accept_quest", eventParam = "quest_kill_slime"
  → QuestTracker.AcceptQuest(questData)
  → OnQuestAccepted event → UI notification

[Đánh Enemy]
  → EnemyBase.HandleDeath()
  → LootSystem.ProcessEnemyDeath() → grant EXP, Gold, loot
  → QuestTracker.ReportKill(enemyData)
  → Check: objective progress? → OnObjectiveProgress event
  → Check: all objectives done? → OnQuestCompleted event

[Turn In]
  → Quay về NPC → dialogue → Event "complete_quest"
  → QuestTracker.TurnIn(questData)
  → Grant: expReward, goldReward, spReward, itemRewards
  → OnQuestTurnedIn event → UI notification

[Level Up]
  → LevelSystem.AddExp() → check threshold
  → OnLevelUp event
  → Player nhận 5 stat points + 1 skill point
  → Mở stat allocation UI
```

---

## 13. Setup Scene Hoàn Chỉnh

### 13.1. Quick Setup (One-Click)

Menu: `RPG > Testing > Setup Vertical Slice Scene`

Tự động tạo:
- Ground plane
- Player (full components)
- 3 Dummy enemies (Standing, Aggressive, Boss)
- 3 NPC placeholders (QuestGiver, Trainer, Merchant)
- Singletons
- Directional Light

### 13.2. Manual Setup (Step-by-Step)

```
BƯỚC 1: Scene Structure
  [RPG_Singletons]     ← RPG > Mega Setup Singletons
  Player               ← RPG > Mega Setup Player
  Environment/
    Ground
    Props
  SpawnZones/
    SpawnZone_Forest   ← RPG > Mega Setup SpawnZone
    SpawnZone_Cave
  NPCs/
    NPC_Elder
    NPC_Trainer
    NPC_Merchant
  Lighting/
    Directional Light

BƯỚC 2: NavMesh
  Window > AI > Navigation > Bake
  Ground + obstacles phải có Navigation Static = true

BƯỚC 3: Data Assets
  Tạo folder: Assets/Data/
    /Weapons/     → WeaponData assets
    /Items/       → ItemData assets
    /Skills/      → SkillData assets
    /Enemies/     → EnemyData assets
    /Quests/      → QuestData assets
    /NPCs/        → NPCData assets
    /Dialogues/   → DialogueData assets
    /Shops/       → ShopData assets
    /LootTables/  → LootTable assets
    /SkillTrees/  → SkillTreeData assets

BƯỚC 4: Wire Data
  - Player > WeaponHandler > startingMainHand = (WeaponData)
  - SpawnZone > VAT_MobSpawner > enemyData = (EnemyData)
  - NPC > npcData = (NPCData)
  - NPCData > greetingDialogue = (DialogueData)
  - NPCData > availableQuests = (QuestData[])
  - EnemyData > lootTable = (LootTable)

BƯỚC 5: Test
  Play → Lock-on (Tab) → Attack (LMB) → Skill (1-4)
  → Nói chuyện NPC (F) → Nhận quest → Đánh mob → Turn in
```

---

## 14. Dummy Enemy Để Test

### 14.1. File

Script: `Assets/Script/RPGModular/Testing/DummyEnemy_VerticalSlice.cs`

### 14.2. 4 Modes

| Mode | HP | Damage | AI | Dùng để test |
|------|-------|--------|-----|-------------|
| StandingDummy | ∞ | 0 | Đứng yên | Damage output, DPS meter |
| PassiveAI | 500 | 0 | Patrol/Flee | Lock-on, chase behavior |
| AggressiveAI | 500 | 15 | Full AI | Full combat loop |
| BossTest | 5000 | 50 | Full AI | Endurance, skill combos |

### 14.3. Quick Setup

1. Tạo Capsule trong scene
2. Gắn `DummyEnemy_VerticalSlice`
3. Gắn `NavMeshAgent`
4. Chọn mode trong Inspector
5. Play → đánh thử

### 14.4. Debug Features

- **Console Log:** Mỗi hit log chi tiết: damage, type, crit/block/dodge, HP còn lại, DPS
- **Color Flash:** Trắng khi bị đánh, xám khi chết
- **Auto-Respawn:** Tự sống lại sau 3s (toggle được)
- **Infinite HP:** Standing Dummy mode luôn full HP
- **Context Menu:** Right-click > Reset Stats / Force Kill / Heal Full

---

## 15. Checklist Vertical Slice

### Phase 1: Foundation
- [ ] Player setup (Mega Setup Player)
- [ ] Singletons setup (Mega Setup Singletons)
- [ ] NavMesh baked
- [ ] Camera setup (CameraController)

### Phase 2: Combat
- [ ] Tạo 1 WeaponData (Sword)
- [ ] Gắn weapon vào Player WeaponHandler
- [ ] Tạo 1 EnemyData (Slime)
- [ ] Tạo Dummy Enemy → test combat
- [ ] Verify: lock-on, auto-attack, combo, damage popup

### Phase 3: Skills
- [ ] Tạo 2-3 SkillData (1 attack, 1 buff, 1 AoE)
- [ ] Tạo SkillTreeData (Blade tree)
- [ ] Test: learn skill, equip to bar, use skill
- [ ] Verify: cooldown, MP cost, damage

### Phase 4: NPC & Quest
- [ ] Tạo NPCData (Quest Giver)
- [ ] Tạo DialogueData (greeting + quest offer)
- [ ] Tạo QuestData (Kill 5 slimes)
- [ ] Wire: NPC → Dialogue → Quest
- [ ] Test: interact, accept quest, kill mobs, turn in
- [ ] Verify: EXP/Gold/Item rewards

### Phase 5: Economy
- [ ] Tạo ItemData (Health Potion, Slime Gel)
- [ ] Tạo ShopData + NPCData (Merchant)
- [ ] Tạo LootTable cho enemy
- [ ] Test: loot drop, buy/sell, use consumable

### Phase 6: Polish
- [ ] HUD Panel (HP/MP/Stamina/Chi bars)
- [ ] Damage Popup
- [ ] Death Panel + Respawn
- [ ] Save/Load test

---

## APPENDIX: Input Mapping

| Key | Action | Ghi chú |
|-----|--------|---------|
| LMB | Attack | Normal attack / auto-attack khi lock-on |
| RMB | Heavy Attack | Damage x1.5 |
| Tab | Lock-On Toggle | Cycle targets |
| 1-4 | Skill 1-4 | Slot 0-3 trên SkillBar |
| Q | Block | Default slot 4 |
| F | Interact | NPC, pickup, door |
| Shift | Sprint | Tốn Stamina |
| W/A/S/D | Movement | Double-tap = Dodge |
| Space | Jump | |

## APPENDIX: Stat Formulas Tóm Tắt

```
MaxHP       = 100 + VIT × 15
MaxMP       = 50  + INT × 12
MaxStamina  = 100 + VIT × 8 + AGI × 4
PhyATK      = STR × 2.0 + DEX × 0.5
MagATK      = INT × 2.5
PhyDEF      = VIT × 1.5 + STR × 0.3
MagDEF      = INT × 1.2 + VIT × 0.5
AtkSpeed    = 1.0 + (AGI-10)×0.02 + (DEX-10)×0.01  [0.5 ~ 2.0]
MoveSpeed   = 5.0 + (AGI-10)×0.15                    [3.0 ~ 10.0]
CritChance  = 5% + DEX×0.5% + LUK×0.3%              [0% ~ 100%]
CritDamage  = 150% + LUK×1.5%
DodgeChance = AGI×0.4% + LUK×0.2%                    [0% ~ 50%]
ParryWindow = 0.15s + TECH×0.005s                     [0.1 ~ 0.5s]
```

## APPENDIX: Damage Pipeline

```
Raw Damage
  → [1] CritProcessor (P=10): roll crit → ×CritDamage
  → [2] DodgeProcessor (P=20): roll dodge → damage = 0
  → [3] BlockProcessor (P=30): blocking? → ×0.3 (normal) or ×0.6 (heavy)
  → [4] DefenseProcessor (P=40): damage × 100/(100+defense)
  → [5] MinDamageProcessor (P=100): min 1 damage
  → Final Damage
```
