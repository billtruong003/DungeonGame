# RPGModular - Hướng Dẫn Setup Vertical Slice

> Hướng dẫn setup đầy đủ một Vertical Slice để test combat, skill, inventory, equipment, NPC, quest.
> Từ zero -> scene chơi được trong ~5 phút.

---

## Mục Lục

1. [Tổng Quan Editor Tools](#1-tổng-quan-editor-tools)
2. [Setup Nhanh (One-Click)](#2-setup-nhanh-one-click)
3. [Setup Thủ Công Chi Tiết](#3-setup-thủ-công-chi-tiết)
   - [3.1 Layers](#31-layers)
   - [3.2 Player](#32-player)
   - [3.3 Singletons](#33-singletons)
   - [3.4 Camera](#34-camera)
   - [3.5 NavMesh](#35-navmesh)
4. [Setup Enemy & Dummy](#4-setup-enemy--dummy)
   - [4.1 Dummy Enemy (Test Combat)](#41-dummy-enemy-test-combat)
   - [4.2 Enemy Thật (EnemyData + EnemyAI)](#42-enemy-thật-enemydata--enemyai)
   - [4.3 Boss Test](#43-boss-test)
5. [Setup Weapon & Equipment](#5-setup-weapon--equipment)
   - [5.1 Tạo WeaponData](#51-tạo-weapondata)
   - [5.2 Tạo ItemData (Armor/Accessory)](#52-tạo-itemdata-armoraccessory)
   - [5.3 Gán Weapon cho Player](#53-gán-weapon-cho-player)
6. [Setup Skill System](#6-setup-skill-system)
   - [6.1 Tạo SkillData](#61-tạo-skilldata)
   - [6.2 Cấu Hình SkillBar](#62-cấu-hình-skillbar)
   - [6.3 Skill Tree & Progression](#63-skill-tree--progression)
7. [Setup Inventory](#7-setup-inventory)
8. [Setup NPC](#8-setup-npc)
   - [8.1 NPC Merchant (Shop)](#81-npc-merchant-shop)
   - [8.2 NPC Quest Giver](#82-npc-quest-giver)
   - [8.3 NPC Trainer](#83-npc-trainer)
9. [Setup Quest](#9-setup-quest)
10. [Setup Loot Table](#10-setup-loot-table)
11. [Test & Debug](#11-test--debug)
12. [Checklist Trước Khi Play](#12-checklist-trước-khi-play)
13. [Xử Lý Lỗi](#13-xử-lý-lỗi)

---

## 1. Tổng Quan Editor Tools

RPGModular có 2 hệ thống Editor tool:

### Tool Mới (Khuyên Dùng)

| Menu Path | Shortcut | Chức Năng |
|-----------|----------|-----------|
| `RPGModular/Setup Wizard` | Ctrl+Shift+R | Cửa sổ 6 tab: Player, Enemy, Camera, Layers, Validate, QuickCreate |
| `RPGModular/Quick Setup Player` | Ctrl+Shift+P | One-click setup player (tự nối + tìm bone + hitbox) |

**File:** `Assets/Script/RPGModular/Editor/RPGModularSetupWizard.cs`

**Ưu điểm so với tool cũ:**
- Tự động nối tất cả dependencies (PlayerController -> CombatSM, LocoSM, Input, v.v.)
- Tự tìm weapon bone slots (Hand.R, Hand.L, Spine) với fuzzy matching
- Tạo hitbox đúng kích thước (MainHand, OffHand, Body)
- Kiểm tra scene (tìm missing references)
- Tạo nhanh assets (WeaponData, EnemyData, Prefab templates)
- Hỗ trợ `[field: SerializeField]` properties (backing field wiring)

### Tool Cũ (Vẫn Dùng Được)

| Menu Path | Chức Năng |
|-----------|-----------|
| `RPG/Mega Setup Player` | Gắn component, KHÔNG nối dependencies |
| `RPG/Mega Setup Singletons` | Tạo [RPG_Singletons] object |
| `RPG/Mega Setup SpawnZone` | Tạo SpawnZone (PackManager + VAT_MobSpawner) |
| `RPG/Setup Quest Tracker on Player` | Gắn QuestTracker |
| `RPG/Validate Player Setup` | Kiểm tra component có đủ không |
| `RPG/Animation Setup Wizard` | Tạo Animator Controller từ animation clips |
| `RPG/Testing/Setup Vertical Slice Scene` | One-click tạo scene test |

**File:** `Assets/Script/RPGModular/Editor/RPGMegaSetup.cs`

### So Sánh

| | RPGMegaSetup (cũ) | RPGModularSetupWizard (mới) |
|-|-------------------|---------------------------|
| Gắn component | Có | Có |
| Nối dependencies | KHÔNG | CÓ (tự động) |
| Tìm weapon bones | KHÔNG | CÓ (fuzzy match) |
| Tạo hitbox | Có (rỗng) | CÓ (đúng size) |
| Enemy setup | KHÔNG | CÓ |
| Camera setup | KHÔNG | CÓ |
| Layer setup | KHÔNG | CÓ |
| Validation | Đơn giản | 20+ checks |
| Quick Create | KHÔNG | CÓ (WeaponData, EnemyData, Prefabs) |

---

## 2. Setup Nhanh (One-Click)

**Cách nhanh nhất để có 1 scene chơi được:**

### Bước 1: Chạy Vertical Slice Setup
```
Menu: RPG > Testing > Setup Vertical Slice Scene
```

Script sẽ tự động tạo:
- Ground (Plane 10x10)
- Player (Capsule + đầy đủ components)
- [RPG_Singletons] (LootSystem, DeathSystem, DialogueSystem, ShopService, v.v.)
- 3 Dummy Enemies: Standing (xanh dương), Aggressive (đỏ), Boss (tím)
- 3 NPCs: QuestGiver (xanh lá), Trainer (cyan), Merchant (vàng)
- Directional Light

### Bước 2: Tạo Layers
```
Menu: RPGModular/Setup Wizard -> Tab "Layers" -> "Create Missing Layers"
```
Cần 5 layers: Player, Enemy, Ground, Interactable, Hitbox

### Bước 3: Tạo WeaponData
```
Right-click Project > Create > RPG > Weapon Data
```
Hoặc dùng Setup Wizard > Tab "Create" > "Create WeaponData Asset"

### Bước 4: Gán Weapon
Chọn Player > Inspector > `WeaponHandler` > kéo WeaponData vào `startingMainHand`

### Bước 5: Bake NavMesh
```
Window > AI > Navigation > Bake
```

### Bước 6: Validate
```
Menu: RPGModular/Setup Wizard -> Tab "Validate" -> "Validate Scene"
```
Sửa bất kỳ error nào trước khi Play.

### Bước 7: Play & Test!

---

## 3. Setup Thủ Công Chi Tiết

### 3.1 Layers

**Bắt buộc tạo trước khi setup bất kỳ entity nào.**

| Layer | Dùng Cho |
|-------|----------|
| Player | Player GameObject |
| Enemy | Tất cả enemy |
| Ground | Mặt đất, terrain |
| Interactable | NPC, rương, cửa |
| Hitbox | Hitbox triggers (attack colliders) |

**Cách tạo:**
- `RPGModular/Setup Wizard` > Tab "Layers" > "Create Missing Layers"
- Hoặc thủ công: Edit > Project Settings > Tags and Layers > thêm vào User Layers

**Physics Matrix (khuyên dùng):**
```
Player     <-> Enemy, Ground, Interactable
Enemy      <-> Player, Ground, Enemy
Hitbox     <-> Player, Enemy (trigger only)
Ground     <-> Player, Enemy
```
Setup tại: Edit > Project Settings > Physics > Layer Collision Matrix

### 3.2 Player

#### Cách 1: Setup Wizard (Khuyên Dùng)

1. Tạo GameObject rỗng trong scene, đặt tên "Player"
2. Tạo child "Model" chứa 3D character model (có Animator component)
   - Nếu chưa có model, dùng Capsule tạm
3. Mở `RPGModular/Setup Wizard` (Ctrl+Shift+R)
4. Tab "Player":
   - Kéo Player root vào "Player Root"
   - Tắt/bật các option theo ý (khuyên để mặc định)
   - Click "Setup Player"
5. Kiểm tra Inspector: ~22 components trên root + AnimationController trên Model child

#### Cách 2: Quick Setup (Nhanh)

1. Tạo Player + Model child (giống trên)
2. Chọn Player trong Hierarchy
3. `RPGModular/Quick Setup Player` (Ctrl+Shift+P)
4. Confirm -> Xong

#### Cách 3: Mega Setup (Cũ, cần nối thủ công)

1. Chọn Player root
2. `RPG/Mega Setup Player`
3. Sau đó cần TỰ TAY nối references trong Inspector cho:
   - PlayerController: locomotion, combat, input, weaponHandler, lockOn, health, autoAttack, animController
   - LocomotionStateMachine: AnimController, Stats, Health, Input, Controller
   - CombatStateMachine: AnimController, Stats, Health, Weapons, CombatLoco, PlayerInput, Hitbox, LockOn, AutoAttack
   - HealthSystem: stats
   - WeaponHandler: stats, animController
   - AutoAttackSystem: weaponHandler, lockOn, stats, animController
   - CombatLocomotion: controller, animController
   - HitboxManager: animController

#### Danh Sách Components Được Gắn

**Trên Root (Player):**

| Component | Nhóm | Chức Năng |
|-----------|------|-----------|
| PlayerCore | Core | Hub truy cập tất cả subsystem |
| CharacterStats | Core | 7 base stats + derived stats + modifiers |
| HealthSystem | Core | HP, Mana, Stamina, Chi + hồi phục |
| PlayerInputHandler | Core | Keybinding + input buffering |
| PlayerController | Core | Cầu nối Exploration <-> Combat mode |
| CharacterController | Core | Unity physics movement |
| LocomotionStateMachine | Di chuyển | Idle, Walk, Run, Sprint, Jump, Dash, Fall |
| CombatStateMachine | Chiến đấu | 10 trạng thái chiến đấu (Idle, Attack, Dodge, v.v.) |
| PlayerDamageHandler | Chiến đấu | Xử lý nhận damage |
| CombatLocomotion | Chiến đấu | Di chuyển trong combat (strafe) |
| LockOnSystem | Chiến đấu | Khóa mục tiêu + chuyển mục tiêu |
| AutoAttackSystem | Chiến đấu | Tự động combo attack khi khóa mục tiêu |
| FocusGauge | Chiến đấu | Thanh tập trung Katana (+50% dmg tối đa) |
| WeaponHandler | Vũ khí | Quản lý tay chính/phụ, trang bị/tháo |
| WeaponVisualHandler | Vũ khí | Spawn visual prefab trên bone |
| HitboxManager | Chiến đấu | Quản lý hitbox triggers |
| Inventory | Phát triển | Lưu trữ vật phẩm + vàng |
| EquipmentSystem | Phát triển | 8 ô trang bị |
| LevelSystem | Phát triển | EXP, Level, Điểm Chỉ Số, Điểm Kỹ Năng |
| StatusEffectSystem | Phát triển | Buff/Debuff + DoT/HoT |
| PlayerSkillBook | Kỹ năng | Kỹ năng đã học + quản lý SP |
| SkillBar | Kỹ năng | 6 slots (4 chủ động + đỡ + đỡ đòn) |
| SkillCaster | Kỹ năng | Luồng cast + tính sát thương |
| ComboTracker | Kỹ năng | Theo dõi combo bonus |

**Trên Child (Model):**

| Component | Chức Năng |
|-----------|-----------|
| Animator | Unity animation |
| AnimationController | RPGModular animation priorities + phases |

**Trên Child (Hitboxes):**

| Component | Chức Năng |
|-----------|-----------|
| HitboxManager | Quản lý cửa sổ hitbox |
| MainHandHitbox | DamageHitbox - vũ khí chính |
| OffHandHitbox | DamageHitbox - tay phụ |
| BodyHitbox | DamageHitbox - thân (shield bash, v.v.) |

**Trên Child (LockOnPoint):**
- Vị trí: (0, 1.2, 0) - điểm để enemy khóa vào player

### 3.3 Singletons

```
Menu: RPG > Mega Setup Singletons
```

Tạo `[RPG_Singletons]` GameObject với:

| Component | Chức Năng |
|-----------|-----------|
| LootSystem | Xử lý item drop từ enemy |
| DeathSystem | Xử lý sự kiện chết |
| DialogueSystem | Quản lý luồng hội thoại |
| ShopService | Mua/Bán vật phẩm |
| ZoneSystem | Chuyển vùng |
| SaveLoadSystem | Lưu/Tải trạng thái game |
| CraftingSystem | Chế tạo vật phẩm |
| WeaponEnhancement | Nâng cấp vũ khí |
| TamerSystem | Hệ thống bắt pet |

### 3.4 Camera

**Cách 1: Setup Wizard**
1. `RPGModular/Setup Wizard` > Tab "Camera"
2. Click "Auto-Detect Main Camera" hoặc kéo Camera vào
3. Kéo Player vào "Target"
4. Click "Setup Camera"

**Cách 2: Tạo Mới**
1. Setup Wizard > Tab "Camera" > "Create New Camera"
2. Sẽ tạo camera tại (0, 3, -6) với CameraController
3. Nếu có Main Camera cũ, sẽ hỏi disable

### 3.5 NavMesh

**BẮT BUỘC để enemy AI hoạt động.**

1. `Window > AI > Navigation`
2. Tab "Bake"
3. Chọn Ground object (hoặc tất cả static objects)
4. Click "Bake"
5. Kiểm tra: mặt đất phải có màu xanh (NavMesh area)

> **Lưu ý:** Mỗi khi thay đổi terrain/ground, phải bake lại NavMesh.

---

## 4. Setup Enemy & Dummy

### 4.1 Dummy Enemy (Test Combat)

**Cách nhanh nhất để test damage output:**

1. Tạo GameObject (Capsule) trong scene
2. Đặt tên: "DummyEnemy_Test"
3. Gắn components:
   - `NavMeshAgent` (speed: 3.5, stoppingDistance: 2)
   - `CapsuleCollider`
   - `DummyEnemy_VerticalSlice`
4. Tag = "Enemy", Layer = "Enemy"
5. Tạo child "LockOnPoint" tại (0, 1.2, 0)

**Script:** `Assets/Script/RPGModular/Testing/DummyEnemy_VerticalSlice.cs`

#### 4 Chế Độ Dummy

| Chế Độ | HP | Damage | AI | Dùng Để Test |
|--------|------|--------|-------|------------|
| **StandingDummy** | Vô hạn | 0 | Tắt | DPS output, sát thương skill |
| **PassiveAI** | 500 | 0 | Tuần tra/Chạy | Lock-on, đuổi theo, chọn mục tiêu |
| **AggressiveAI** | 500 | 15 | Đầy đủ AI | Vòng lặp chiến đấu hoàn chỉnh |
| **BossTest** | 5000 | 50 | Đầy đủ AI | Trận đánh lâu dài, test boss |

#### Cấu Hình Trong Inspector

```
=== CẤU HÌNH DUMMY ===
Mode:              [Chọn chế độ]
Auto Respawn:      true (tự hồi sinh sau 3s)
Respawn Delay:     3
Show Damage Log:   true (log chi tiết trong Console)
Infinite HP:       false (tự bật với StandingDummy)

=== PHẢN HỒI HÌNH ẢNH ===
Normal Color:      (màu mặc định)
Hit Color:         White (nhấp nháy khi bị đánh)
Dead Color:        Gray

=== GHI ĐÈ CHẾ ĐỘ BOSS ===
Boss HP:           5000
Boss Damage:       50
Boss Attack CD:    1.5

=== THÔNG TIN DEBUG (Chỉ Đọc) ===
Total Hits Taken:  (tự đếm)
Total Damage:      (tự đếm)
DPS Tracker:       (DPS thời gian thực)
Last Skill Hit:    (tên skill cuối)
```

#### Output Console Debug

Mỗi hit sẽ log:
```
[DummyEnemy] HIT #5: 127.3 dmg [CRIT] | Type: Slash | Heavy: False | HP: 373/500 (75%) | From: Player | DPS: 85.2
```

#### Context Menu (Chuột phải vào script trong Inspector)
- **Reset Stats** - Xóa bộ đếm
- **Force Kill** - Giết ngay
- **Heal Full** - Hồi max HP

#### Tự Tạo EnemyData

Nếu không gán EnemyData, script sẽ **tự động tạo** với:
- HP: 500, Damage: 15, Speed: 3.5
- Attack Range: 2.5, Cooldown: 2s
- Detection: 15m
- Phòng thủ: 10 vật lý / 5 phép
- Phần thưởng: 100 EXP, 50 Gold

### 4.2 Enemy Thật (EnemyData + EnemyAI)

**Khi cần enemy có đầy đủ AI và dữ liệu:**

#### Bước 1: Tạo EnemyData Asset
```
Right-click Project > Create > Game > Enemy Data
```

Cấu hình trong Inspector:

```
=== Danh Tính ===
Enemy ID:          "goblin_warrior"
Name Key:          "Goblin Warrior"
Tier:              Normal / Elite / MiniBoss / Boss

=== Chỉ Số ===
Base Level:        5
Base HP:           300
Base Damage:       20
Move Speed:        4.0
Physical Defense:  15
Magic Defense:     5
Damage Type:       Slash

=== Chiến Đấu ===
Attack Range:      2.5
Attack Cooldown:   2.0
Detection Range:   12
Dodge Chance:      0.05
Block Chance:      0.1

=== Phần Thưởng ===
EXP Reward:        50
Gold Reward:       25
Loot Table:        (gán LootTable asset)

=== Hành Vi Bầy Đàn ===
Preferred Pack Size: 3
Aggro Radius:      0 (0 = dùng detection range)
```

#### Bước 2: Setup Enemy GameObject

**Cách 1: Setup Wizard**
1. Tạo GameObject với 3D model (có Animator child)
2. `RPGModular/Setup Wizard` > Tab "Enemy"
3. Kéo enemy root vào, chọn/tạo EnemyData
4. Click "Setup Enemy"

**Cách 2: Thủ Công**
1. Tạo GameObject "Enemy_Goblin"
2. Child "Model" có Animator
3. Gắn components:
   - `EnemyBase` -> kéo EnemyData vào
   - `EnemyAI` -> Initialize sẽ lấy từ EnemyData
   - `NavMeshAgent` (speed từ EnemyData)
   - `CapsuleCollider` (radius: 0.4, height: 1.8)
4. Tạo child "LockOnPoint" tại (0, 1.2, 0)
5. Tag = "Enemy", Layer = "Enemy"

#### Các Trạng Thái AI của Enemy

```
Idle -> Patrol (đi loanh quanh trong bán kính tuần tra)
     -> Alert (phát hiện player, quay mặt lại)
     -> Chase (đuổi theo, NavMesh pathfinding)
     -> Attack (trong tầm đánh, dựa trên cooldown)
     -> Retreat (quá xa điểm spawn, quay về)
     -> Flee (HP thấp, chạy ngược lại 5s)
     -> ReactiveDefend (phòng thủ 3s)
     -> Dead
```

### 4.3 Boss Test

Tạo boss với DummyEnemy_VerticalSlice:
1. Mode: **BossTest**
2. Boss HP: 5000 (hoặc cao hơn)
3. Boss Damage: 50
4. Boss Attack CD: 1.5s
5. EnemyData.tier: Boss
6. Phòng thủ: 30 vật lý / 20 phép
7. Dodge: 10%

Hoặc tạo EnemyData riêng cho boss với stats cao hơn và loot table tốt hơn.

---

## 5. Setup Weapon & Equipment

### 5.1 Tạo WeaponData

```
Right-click Project > Create > RPG > Weapon Data
```

Hoặc: Setup Wizard > Tab "Create" > "Create WeaponData Asset"

#### Cấu Hình WeaponData

```
=== Cơ Bản ===
Weapon Name:       "Iron Sword"
Type:              Sword (chọn từ 15 loại)
Slot:              MainHand / OffHand
Icon:              (sprite)
Weapon Prefab:     (3D model prefab - tùy chọn)

=== Sát Thương ===
Primary Damage Type: Slash
Base Damage:       25
Attack Range:      2.5
Attack Speed Mod:  1.0

=== Bonus ===
Stat Bonuses:      (thêm STR +5, v.v.)
Requirements:      (cần STR >= 10, v.v.)
```

#### 15 Loại Vũ Khí

| Loại | Slot | Đặc Biệt |
|------|------|----------|
| Unarmed | MainHand | Mặc định khi không có vũ khí |
| Sword | MainHand | Cân bằng, combo 3 hits |
| GreatSword | MainHand | Chậm, sát thương cao |
| Shield | OffHand | Đỡ đòn, dùng với Sword |
| Spear | MainHand | Tầm xa |
| Halberd | MainHand | Tầm xa, sát thương cao |
| Bow | MainHand | Tầm xa (bắn) |
| Bowgun | MainHand | Tầm xa, nhanh hơn Bow |
| Staff | MainHand | Vũ khí phép |
| MagicDevice | OffHand | Hỗ trợ phép |
| Dagger | MainHand | Nhanh, sát thương thấp |
| Knuckle | MainHand | Combo nhanh |
| Katana | MainHand | **Kích hoạt FocusGauge** (+50% dmg tối đa) |
| DualWield | MainHand | 2 vũ khí cùng lúc |
| Axe | MainHand | Chậm, sát thương cao |

#### Tổ Hợp Vũ Khí (Tự Nhận Diện)

```
Sword + Shield   -> Shield_Block animation set
Sword + Sword    -> DualWield animation set
Katana + Nothing -> FocusGauge được kích hoạt
```

### 5.2 Tạo ItemData (Armor/Accessory)

```
Right-click Project > Create > Game > Item Data
```

#### Cấu Hình Trang Bị

```
=== Cơ Bản ===
Item Name:         "Iron Helmet"
Item Type:         Armor
Rarity:            Uncommon
Max Stack:         1
Is Equippable:     true
Default Slot:      Head

=== Bonus Trang Bị ===
Stat Bonuses:
  - VIT +5 (Flat)
  - DEF +10% (PercentAdd)

=== Yêu Cầu ===
Stat Requirements:
  - Level >= 5
  - STR >= 8
```

#### 8 Ô Trang Bị

| Slot | Vị Trí |
|------|--------|
| Head | Mũ/nón |
| Body | Áo giáp |
| Legs | Quần |
| Feet | Giày |
| MainHand | Vũ khí chính |
| OffHand | Khiên/vũ khí phụ |
| Accessory1 | Nhẫn/vòng 1 |
| Accessory2 | Nhẫn/vòng 2 |

### 5.3 Gán Weapon Cho Player

1. Chọn Player trong Hierarchy
2. Inspector > `WeaponHandler`:
   - `Starting Main Hand` -> kéo WeaponData vào
   - `Starting Off Hand` -> kéo Shield/OffHand weapon (tùy chọn)
   - `Main Hand Slot` -> transform trên tay phải (tự tìm bởi Wizard)
   - `Off Hand Slot` -> transform trên tay trái
   - `Main Hand Sheath` -> transform trên lưng (vị trí cất vũ khí)
   - `Off Hand Sheath` -> transform trên lưng

> **Mẹo:** Nếu dùng Setup Wizard, bone slots sẽ được tự động tìm và gán.

---

## 6. Setup Skill System

### 6.1 Tạo SkillData

```
Right-click Project > Create > Game > Skill Data
```

#### Cấu Hình Skill

```
=== Cơ Bản ===
Skill Name:        "Fireball"
Category:          Active
Skill Tree:        Staff (hoặc Universal)
Tier:              1
Max Level:         5
SP Cost Per Level: [1, 1, 2, 2, 3]

=== Chi Phí ===
Base MP Cost:      30
Base Chi Cost:     0
Cast Time:         0.5 (0 = tức thì)
Cooldown:          8

=== Sát Thương ===
Base Power:        80
Power Per Level:   15
Scale Type:        Magical
Primary Scale Stat: INT
Scale Ratio:       1.5
Is Heavy Attack:   false
Has Super Armor:   false

=== Mục Tiêu ===
Target Type:       AoE_Circle
Range:             10
AoE Radius:        3
Cone Angle:        0

=== Hiệu Ứng ===
Applied Effect:    (StatusEffectData - tùy chọn)
Buff Duration:     5

=== Điều Kiện ===
Prerequisites:     (SkillData + level yêu cầu)

=== Animation ===
Animation Clip:    (tùy chọn)
Combo Window:      0.3
```

#### Phân Loại Skill

| Loại | Cách Hoạt Động |
|------|---------------|
| **Active** | Cast được, tốn MP/Chi, có cooldown |
| **Passive** | Tự động áp dụng stat bonus khi học |

#### Loại Mục Tiêu Skill

```
Self, SingleTarget, AoE_Circle, AoE_Cone, AoE_Line,
AoE_Around, Projectile, Party
```

### 6.2 Cấu Hình SkillBar

Player có **6 skill slots**:

| Slot | Mặc Định | Phím |
|------|----------|------|
| Slot 1 | (trống) | Skill1 key |
| Slot 2 | (trống) | Skill2 key |
| Slot 3 | (trống) | Skill3 key |
| Slot 4 | (trống) | Skill4 key |
| Block | Block Skill (tự động) | Block key |
| Parry | Parry Skill (tự động) | Parry key |

**Gán skill:**
1. Chọn Player > Inspector > `SkillBar`
2. Kéo SkillData vào các slot

**Hoặc runtime qua code:**
```csharp
Game.SkillBar.AssignSkill(slotIndex, skillData);
```

### 6.3 Skill Tree & Progression

**LevelSystem** cung cấp:
- **Điểm Chỉ Số:** 5 mỗi level (tăng base stats: STR, INT, AGI, v.v.)
- **Điểm Kỹ Năng:** 1 mỗi level (học skills)

**Công Thức EXP:** `floor(100 * level^1.5)`

| Level | EXP Cần | Tổng Điểm Chỉ Số | Tổng Điểm Kỹ Năng |
|-------|---------|-------------------|-------------------|
| 1 | 100 | 5 | 1 |
| 2 | 283 | 10 | 2 |
| 5 | 1118 | 25 | 5 |
| 10 | 3162 | 50 | 10 |

**Học skill runtime:**
```csharp
Game.SkillBook.LearnSkill(skillData);       // Học skill mới
Game.SkillBook.UpgradeSkill(skillData);     // Tăng level
Game.SkillBook.ResetAllSkills(goldCost);    // Reset (trả hết SP)
```

---

## 7. Setup Inventory

Inventory tự động được gắn bởi setup tools. Cấu hình trong Inspector:

```
Player > Inventory:
  Slot Count:      30 (số slot mặc định)
  Starting Gold:   100

Player > EquipmentSystem:
  (8 ô trang bị tự động)
```

**Thêm starting items (code):**
```csharp
Game.Inv.AddItem(itemData, quantity);
Game.Inv.AddGold(500);
```

**Tạo Item Data cho consumable:**
```
Right-click > Create > Game > Item Data

Item Type:         Consumable
Heal Amount:       100
Mana Amount:       50
Max Stack:         20
```

---

## 8. Setup NPC

### 8.1 NPC Merchant (Shop)

#### Bước 1: Tạo ShopData
```
Right-click > Create > Game > Shop Data
```

```
Name Key:              "General Store"
Buy Price Multiplier:  1.0
Sell Price Multiplier: 0.5
Items:
  - Item: Iron Sword,   Price: 100,  Stock: 5
  - Item: Health Potion, Price: 20,   Stock: -1 (vô hạn)
  - Item: Iron Helmet,   Price: 80,   Stock: 3
```

#### Bước 2: Tạo NPCData
```
Right-click > Create > Game > NPC Data
```

```
NPC ID:        "merchant_01"
Name Key:      "Thương Nhân Vương"
Role:          Merchant
Shop Data:     (kéo ShopData vào)
Greeting:      "Chào mừng! Mua gì nào?"
```

#### Bước 3: Tạo NPC GameObject

1. Tạo GameObject (3D model hoặc Cylinder tạm)
2. Đặt tên "NPC_Merchant"
3. Layer = "Interactable"
4. Gắn component NPC (nếu có) hoặc script tương tác
5. Kéo NPCData vào

### 8.2 NPC Quest Giver

#### Bước 1: Tạo QuestData (xem Mục 9)

#### Bước 2: Tạo NPCData
```
NPC ID:           "quest_giver_01"
Name Key:         "Trưởng Làng"
Role:             QuestGiver
Available Quests: [QuestData_1, QuestData_2]
Greeting:         "Ta cần sự giúp đỡ của ngươi..."
```

#### Bước 3: Tạo GameObject (giống Merchant)

### 8.3 NPC Trainer

```
NPC ID:        "trainer_01"
Name Key:      "Kiếm Sư Lý"
Role:          Trainer
Greeting:      "Muốn học kiếm thuật?"
```

Trainer NPC thường dùng để:
- Mở Skill Tree UI
- Reset skills
- Học special skills

---

## 9. Setup Quest

```
Right-click > Create > Game > Quest Data
```

#### Cấu Hình Quest

```
=== Cơ Bản ===
Quest ID:          "quest_kill_goblins"
Name Key:          "Diệt Goblin"
Desc Key:          "Giết 5 Goblin trong rừng phía Bắc"
Quest Type:        Side (Main / Side / Daily / Weekly)
Required Level:    3
Is Repeatable:     false

=== Mục Tiêu ===
Objectives:
  - Type: Kill
    Target ID: "goblin_warrior"
    Required Amount: 5
    Description: "Giết 5 Goblin Warrior"
    
  - Type: Collect
    Target ID: "goblin_ear"
    Required Amount: 3
    Description: "Thu thập 3 Tai Goblin"

=== Phần Thưởng ===
EXP Reward:        200
Gold Reward:       100
SP Reward:         1
Item Rewards:
  - Iron Sword x1
  - Health Potion x5

=== Điều Kiện ===
Prerequisite Quests: [] (quest phải hoàn thành trước)
```

#### Loại Mục Tiêu Quest

| Loại | Mô Tả |
|------|-------|
| Kill | Giết X con enemy (theo enemyID) |
| Collect | Thu thập X vật phẩm |
| Talk | Nói chuyện với NPC |
| Reach | Đến vị trí/vùng |
| Craft | Chế tạo X vật phẩm |
| Capture | Bắt X pet/quái vật |

---

## 10. Setup Loot Table

```
Right-click > Create > Game > Loot Table
```

```
Entries:
  - Item: Health Potion,  Min Qty: 1, Max Qty: 3, Drop Chance: 0.6  (60%)
  - Item: Iron Ore,       Min Qty: 1, Max Qty: 2, Drop Chance: 0.3  (30%)
  - Item: Rare Sword,     Min Qty: 1, Max Qty: 1, Drop Chance: 0.05 (5%)
  - Item: Goblin Ear,     Min Qty: 1, Max Qty: 1, Drop Chance: 1.0  (100%)
```

**Gán vào EnemyData:**
```
EnemyData > Loot Table > (kéo LootTable asset vào)
```

Khi enemy chết, LootSystem sẽ tự động roll loot table và thêm vào Inventory.

---

## 11. Test & Debug

### Phím Điều Khiển (Mặc Định)

| Phím | Hành Động |
|------|-----------|
| WASD | Di chuyển |
| Chuột | Camera |
| Click trái | Tấn công |
| Click phải (giữ) | Đòn nặng |
| Tab | Bật/tắt Lock-On |
| Shift (giữ) | Chạy nhanh |
| Space | Nhảy |
| Nhấn đúp hướng | Né tránh |
| 1, 2, 3, 4 | Các slot kỹ năng |
| F | Tương tác |
| Q | Đỡ đòn (Block) |
| E | Đỡ phản (Parry) |

### Debug Console

Bật `Show Damage Log` trên DummyEnemy để xem:
```
[DummyEnemy] HIT #1: 45.0 dmg | Type: Slash | Heavy: False | HP: 455/500 (91%) | From: Player | DPS: 45.0
[DummyEnemy] HIT #2: 92.3 dmg [CRIT] | Type: Slash | Heavy: False | HP: 363/500 (73%) | From: Player | DPS: 68.7
[DummyEnemy] DEAD! Total hits: 8 | Total damage: 500 | Avg DPS: 83.3
[DummyEnemy] +100 EXP
[DummyEnemy] +50 Gold
[DummyEnemy] RESPAWNED at (5, 0, 5)
```

### Gizmos (Scene View)

Khi chọn Dummy Enemy:
- **Vòng vàng:** Phạm vi phát hiện (15m)
- **Vòng đỏ:** Tầm đánh (2.5m)
- **Nhãn:** Chế độ + HP hiện tại

### Context Menu - Hành Động Nhanh

Chuột phải vào `DummyEnemy_VerticalSlice` trong Inspector:
- **Reset Stats** - Đặt lại bộ đếm
- **Force Kill** - Giết ngay (test chết/hồi sinh)
- **Heal Full** - Hồi max HP

### Kiểm Tra Scene

```
RPGModular/Setup Wizard > Tab "Validate" > "Validate Scene"
```

Kiểm tra 20+ vấn đề:
- Thiếu components
- References chưa gán (PlayerController.locomotion = null?)
- Thiếu layers
- Nối sai giữa CombatSM <-> AnimController
- v.v.

---

## 12. Checklist Trước Khi Play

```
[ ] Layers đã tạo: Player, Enemy, Ground, Interactable, Hitbox
[ ] Player có đầy đủ components (chạy Validate)
[ ] Player tag = "Player", layer = "Player"
[ ] Player có CharacterController
[ ] Player > Model child có Animator + AnimationController
[ ] Player > WeaponHandler có starting weapon (WeaponData)
[ ] Player > HealthSystem.stats = CharacterStats (tự nối)
[ ] [RPG_Singletons] tồn tại trong scene
[ ] NavMesh đã bake (mặt đất có màu xanh)
[ ] Enemy tag = "Enemy", layer = "Enemy"
[ ] Enemy có NavMeshAgent
[ ] Enemy có EnemyData (hoặc dùng DummyEnemy tự tạo)
[ ] Camera có CameraController + target = Player
[ ] Physics Matrix đúng (Player <-> Enemy, Hitbox <-> Enemy)
[ ] Chạy "Validate Scene" -> 0 errors
```

---

## 13. Xử Lý Lỗi

### Player Không Di Chuyển
- Kiểm tra `CharacterController` có trên Player root
- Kiểm tra `PlayerInputHandler` đã gắn
- Kiểm tra `LocomotionStateMachine` đã nối `Controller` (CharacterController)
- **Sửa nhanh:** Chạy `RPGModular/Quick Setup Player` lại

### Enemy Không Di Chuyển
- Kiểm tra NavMesh đã bake chưa
- Kiểm tra `NavMeshAgent` trên Enemy
- Kiểm tra enemy đứng trên NavMesh area (không lơ lửng)
- Kiểm tra `EnemyAI` enabled = true

### Lock-On Không Hoạt Động
- Kiểm tra enemy có child `LockOnPoint`
- Kiểm tra enemy layer = "Enemy"
- Kiểm tra `LockOnSystem` đã nối

### Đánh Không Ra Damage
- Kiểm tra `HitboxManager` đã nối `mainHandHitbox`
- Kiểm tra hitbox có `DamageHitbox` component
- Kiểm tra hitbox layer = "Hitbox"
- Kiểm tra `WeaponHandler` có weapon (WeaponData)
- Kiểm tra Physics Matrix: Hitbox <-> Enemy đã bật

### Animation Không Chạy
- Kiểm tra `AnimationController` trên child có Animator
- Kiểm tra Animator có AnimatorController asset
- Dùng `RPG/Animation Setup Wizard` để tạo Animator Controller

### Skill Không Cast Được
- Kiểm tra `SkillBar` có skill được gán
- Kiểm tra đủ MP/Chi (HealthSystem resources)
- Kiểm tra cooldown đã hết
- Kiểm tra `SkillCaster` đã nối

### NullReferenceException Liên Tục
- Chạy **Validate Scene** để tìm references thiếu
- Chạy lại **Quick Setup Player** để tự nối
- Kiểm tra `[RPG_Singletons]` có trong scene (Game.cs cần nó)

---

## Tham Chiếu Menu Tạo ScriptableObject

| Đường Dẫn Menu | Tạo Cái Gì |
|----------------|------------|
| `Create > RPG > Weapon Data` | WeaponData |
| `Create > Game > Enemy Data` | EnemyData |
| `Create > Game > Item Data` | ItemData |
| `Create > Game > Item Database` | ItemDatabase |
| `Create > Game > Skill Data` | SkillData |
| `Create > Game > Skill Database` | SkillDatabase |
| `Create > Game > Skill Tree Data` | SkillTreeData |
| `Create > Game > Status Effect Data` | StatusEffectData |
| `Create > Game > Loot Table` | LootTable |
| `Create > Game > NPC Data` | NPCData |
| `Create > Game > Shop Data` | ShopData |
| `Create > Game > Quest Data` | QuestData |
| `Create > Game > Dialogue Data` | DialogueData |
| `Create > Game > Recipe Data` | RecipeData |
| `Create > Game > Pet Data` | PetData |
| `Create > Game > Zone Data` | ZoneData |
| `Create > Game > Localization Config` | LocalizationConfig |

---

## Tham Chiếu Công Thức Chỉ Số

### 7 Chỉ Số Gốc
`STR, INT, AGI, DEX, VIT, LUK, TECH`

### Chỉ Số Phái Sinh

| Chỉ Số | Công Thức |
|--------|-----------|
| HP Tối Đa | baseHP + VIT * 15 |
| Mana Tối Đa | baseMana + INT * 12 |
| Stamina Tối Đa | base + AGI * 8 |
| Tấn Công Vật Lý | STR * 2 + DEX * 0.5 |
| Tấn Công Phép | INT * 2.5 |
| Phòng Thủ Vật Lý | VIT * 1.5 + STR * 0.3 |
| Phòng Thủ Phép | INT * 1.2 + VIT * 0.5 |
| Tỷ Lệ Chí Mạng | 5% + DEX * 0.5% + LUK * 0.3% |
| Sát Thương Chí Mạng | 150% (mặc định) |
| Tỷ Lệ Né | AGI * 0.4% + LUK * 0.2% (giới hạn 50%) |
| Tốc Độ Tấn Công | 1.0 + AGI * 0.005 |

### Damage Pipeline (theo thứ tự)

```
1. CritProcessor      (Ưu tiên 10) - Tung xúc xắc chí mạng, nhân hệ số chí mạng
2. DodgeProcessor     (Ưu tiên 20) - Tung xúc xắc né, damage = 0 nếu né
3. BlockProcessor     (Ưu tiên 30) - Giảm damage nếu đang đỡ
4. DefenseProcessor   (Ưu tiên 40) - damage = damage * 100/(100+DEF)
5. MinDamageProcessor (Ưu tiên 100) - Đảm bảo tối thiểu 1 damage
```

### Loại Modifier

| Loại | Thứ Tự | Ví Dụ |
|------|--------|-------|
| Flat | Cộng trước | +10 STR |
| PercentAdd | Cộng % trước | +15% STR |
| PercentMult | Nhân % sau | x1.2 STR |

**Công thức:** `giáTrịCuối = (giáTrịGốc + tổngFlat) * (1 + tổngPercentAdd) * tíchCủa(1 + percentMult)`

---

> **Cập nhật lần cuối:** 15/04/2026
> **Phiên bản RPGModular:** Hiện tại (nhánh main)
