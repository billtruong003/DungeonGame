# Vertical Slice — Hướng Dẫn Setup Chi Tiết
### Từ 0 đến Playable Demo | Tất cả những gì cần chuẩn bị

---

## MỤC TIÊU VERTICAL SLICE

Bản demo chơi được hoàn chỉnh 1 vòng gameplay:
```
Thị trấn → Ra cánh đồng → Đánh bầy quái (auto-attack + skill combo) →
Loot EXP/Vàng/Item → Level up → Phân chỉ số → Trang bị →
Đánh Boss → Loot hiếm → Quay về thị trấn → Bán đồ → Lặp lại
```

---

## PHẦN 1: MODEL 3D CẦN CHUẨN BỊ

### 1.1 Player Model (1 model)

| Yêu cầu | Chi tiết |
|----------|---------|
| **Model** | Nhân vật humanoid, phong cách anime/wuxia |
| **Poly** | 5.000-15.000 tri (mobile-friendly) |
| **Rig** | Humanoid rig chuẩn Unity (Mixamo compatible) |
| **Material** | 1-2 material, dùng ToonLit shader của project |
| **Bone quan trọng** | `RightHand` (gắn vũ khí main), `LeftHand` (gắn khiên/off-hand), `Spine` (gắn sheath) |

**Nguồn gợi ý:**
- Mixamo (miễn phí, auto-rig)
- Unity Asset Store: "Low Poly Character", "Anime Character"
- Tự model bằng Blender

### 1.2 Vũ Khí Model (tối thiểu 1, khuyến nghị 3)

| # | Vũ khí | Mô tả | Gắn vào |
|---|--------|-------|---------|
| 1 | **Kiếm 1 tay** | Sword cơ bản, MVP bắt buộc | RightHand |
| 2 | Đại kiếm (tùy) | GreatSword, 2H | RightHand |
| 3 | Cung (tùy) | Bow, ranged | LeftHand |

**Yêu cầu mỗi vũ khí:**
- Prefab riêng, pivot point ở tay cầm
- 500-2000 tri
- Collider (Box/Capsule) cho hitbox
- 1 material

### 1.3 Enemy Models (tối thiểu 3 + 1 boss)

| # | Quái | Phong cách | Poly | Ghi chú |
|---|------|-----------|------|---------|
| 1 | **Nhớt Xanh (Slime)** | Đơn giản, blob | 500-1000 | Dễ nhất, test đầu tiên |
| 2 | **Sói Hoang** | Quadruped, nhanh | 2000-4000 | Cần rig 4 chân |
| 3 | **Yêu Tinh (Goblin)** | Humanoid nhỏ | 2000-4000 | Dùng Mixamo rig |
| 4 | **Cổ Long (Boss)** | Rồng, lớn | 5000-10000 | Nhiều animation nhất |

**Quan trọng: Enemy dùng VAT (Vertex Animation Texture)**
```
Quy trình cho mỗi enemy:
1. Model + Rig trong Blender/Maya
2. Tạo animation clips (xem phần Animation)
3. Import vào Unity → bake bằng VAT Baker tool
4. Baker sinh ra: VAT mesh + position texture + (optional) normal texture
5. Gắn VAT_Animator component, assign clips
6. KHÔNG cần SkinnedMeshRenderer lúc runtime → dùng MeshRenderer + VAT shader
```

### 1.4 Environment (2 scene)

| # | Scene | Mô tả | Asset cần |
|---|-------|-------|----------|
| 1 | **Thị trấn** | Hub an toàn, NPC | Terrain/flat ground, 3-4 NPC model, building cơ bản |
| 2 | **Đồng Cỏ Xanh** | Khu vực chiến đấu level 1-10 | Terrain, cỏ, cây, 3 SpawnZone, 1 Boss zone, Portal |

**Tối thiểu:**
- 1 terrain hoặc flat plane mỗi scene
- Portal trigger (empty GO + Box Collider + Portal script)
- SpawnZone GO (empty GO + PackManager + VAT_MobSpawner)

---

## PHẦN 2: ANIMATION CẦN CHUẨN BỊ

### 2.1 Player Animation — MVP (29 clips)

**Nguồn: Mixamo.com (miễn phí, download FBX)**

#### Exploration (10 clips — dùng chung mọi vũ khí)

| # | Tên clip cần đặt | Mixamo search keyword | Ghi chú |
|---|------------------|----------------------|---------|
| 1 | `Explore_Idle` | "Idle" hoặc "Breathing Idle" | Loop |
| 2 | `Explore_Walk` | "Walking" | Loop |
| 3 | `Explore_Run` | "Running" hoặc "Jogging" | Loop |
| 4 | `Explore_Sprint` | "Fast Run" hoặc "Sprinting" | Loop |
| 5 | `Explore_Jump` | "Jump" | Không loop |
| 6 | `Explore_DoubleJump` | "Flip" hoặc "Jump" (khác clip) | Không loop |
| 7 | `Explore_Fall` | "Falling Idle" | Loop |
| 8 | `Explore_Land_Soft` | "Landing" | Không loop, ngắn |
| 9 | `Explore_Land_Hard` | "Hard Landing" | Không loop |
| 10 | `Explore_Dash` | "Dodge Forward" | Không loop |

#### Combat Sword (13 clips — weapon type đầu tiên)

| # | Tên clip cần đặt | Mixamo search keyword | Ghi chú |
|---|------------------|----------------------|---------|
| 1 | `Sword_Idle` | "Sword Idle" | Loop |
| 2 | `Sword_Walk_Fwd` | "Sword Walk" | Loop |
| 3 | `Sword_Walk_Back` | "Walking Backward" | Loop |
| 4 | `Sword_Walk_Left` | "Left Strafe Walk" | Loop |
| 5 | `Sword_Walk_Right` | "Right Strafe Walk" | Loop |
| 6 | `Sword_Atk1` | "Sword Slash" | Không loop, combo đòn 1 |
| 7 | `Sword_Atk2` | "Sword Slash 2" hoặc "Slash" | Không loop, combo đòn 2 |
| 8 | `Sword_Atk3` | "Overhead Slash" hoặc "Stab" | Không loop, combo đòn 3 |
| 9 | `Sword_Hit_Light` | "Hit Reaction" hoặc "Flinch" | Không loop, ngắn ~0.3s |
| 10 | `Sword_Hit_Heavy` | "Big Hit" hoặc "Knockback" | Không loop |
| 11 | `Sword_Knockback` | "Stumble Backwards" | Không loop |
| 12 | `Sword_Equip` | "Draw Sword" | Không loop |
| 13 | `Sword_Unequip` | "Sheathe Sword" | Không loop |

#### Combat Chung (6 clips)

| # | Tên clip cần đặt | Mixamo search keyword | Ghi chú |
|---|------------------|----------------------|---------|
| 1 | `Dodge_Fwd` | "Dodge Forward" | Không loop |
| 2 | `Dodge_Back` | "Dodge Backward" | Không loop |
| 3 | `Dodge_Left` | "Dodge Left" | Không loop |
| 4 | `Dodge_Right` | "Dodge Right" | Không loop |
| 5 | `Death` | "Dying" hoặc "Death" | Không loop |
| 6 | `Skill_Charge` | "Charge" hoặc "Power Up" | Loop (charge hold) |

### 2.2 Enemy Animation — MVP

#### Slime (5 clips — đơn giản nhất, có thể tự làm)
```
Idle     — nhún nhún tại chỗ (scale oscillation cũng được)
Walk     — nhảy nhún di chuyển
Attack1  — lao tới / đập
Hit      — co lại
Death    — tan ra / xẹp xuống
```

#### Wolf (5 clips — Mixamo "Wolf" pack hoặc tự rig)
```
Idle     — đứng thở
Walk     — chạy 4 chân
Attack1  — cắn / vồ
Hit      — flinch
Death    — ngã xuống
```

#### Goblin (5 clips — Mixamo humanoid)
```
Idle     — đứng nervously
Walk     — chạy lom khom
Attack1  — đánh gậy / ném đá
Hit      — flinch
Death    — ngã
```

#### Boss: Cổ Long (8 clips)
```
Idle     — đứng uy nghi
Walk     — bước chậm, nặng nề
Attack1  — vuốt chân trước
Attack2  — phun lửa (AoE cone)
Attack3  — quét đuôi (AoE circle)
Hit      — flinch nhẹ (boss ít bị flinch)
Death    — gục xuống
Phase    — gầm rú chuyển giai đoạn (VFX trigger)
```

### 2.3 Tổng Hợp Animation MVP

```
Player Exploration:     10 clips
Player Combat Sword:    13 clips
Player Combat Chung:     6 clips
────────────────────────────────
Player tổng:            29 clips

Slime:                   5 clips
Wolf:                    5 clips
Goblin:                  5 clips
Boss (Cổ Long):          8 clips
────────────────────────────────
Enemy tổng:             23 clips

═══════════════════════════════
TỔNG CỘNG MVP:          52 clips
═══════════════════════════════
```

---

## PHẦN 3: SETUP TỪNG BƯỚC

### Bước 1: Import Model + Animation

```
1. Download animation từ Mixamo → FBX format, "Without Skin" (dùng chung 1 model)
2. Import vào Unity:
   Assets/
   ├── Models/
   │   ├── Player/
   │   │   ├── PlayerModel.fbx          ← model + rig
   │   │   └── Animations/              ← tất cả FBX animation
   │   │       ├── Explore_Idle.fbx
   │   │       ├── Explore_Walk.fbx
   │   │       ├── Sword_Atk1.fbx
   │   │       └── ... (29 file)
   │   ├── Weapons/
   │   │   └── Sword.fbx
   │   └── Enemies/
   │       ├── Slime/
   │       ├── Wolf/
   │       ├── Goblin/
   │       └── Boss_Dragon/

3. Trong Inspector mỗi animation FBX:
   - Tab Rig → Animation Type = Humanoid (player)
   - Tab Animation → đặt tên clip đúng convention
   - Tick "Loop Time" cho các clip loop (Idle, Walk, Run...)

4. Tạo Animator Controller:
   Assets/Animations/PlayerAnimator.controller
   - Flat state machine (không dùng sub-state)
   - Tạo state cho mỗi clip, tên = tên clip
   - Parameters: MoveSpeed(float), MoveX(float), MoveY(float),
                  IsGrounded(bool), InCombat(bool)
```

### Bước 2: VAT Bake Enemy

```
1. Mỗi enemy model import vào Unity với SkinnedMeshRenderer
2. Gắn tất cả animation clips lên Animator
3. Menu: VAT > Bake
   - Chọn SkinnedMeshRenderer
   - Chọn animation clips cần bake
   - Output: VAT mesh + position texture
4. Tạo prefab từ VAT output:
   - MeshRenderer + MeshFilter (VAT mesh)
   - Material dùng ToonLit_VAT shader
   - Gắn VAT_Animator component
   - Gắn collider (Capsule cho humanoid, Box cho slime)
   - Gắn EnemyBase + EnemyAI component
5. Tạo EnemyData SO:
   - Menu: Assets > Create > Game > Enemy Data
   - Điền stats, animation clip names, rewards
```

### Bước 3: Setup Player trong Scene

```
1. Kéo PlayerModel vào scene
2. Chọn Player GO → Menu: RPG > Mega Setup Player
   → Tự gắn 21+ components
   → Tạo child: Hitboxes, LockOnPoint
3. Gắn Animator Controller vào child model (có Animator)
4. Tạo WeaponData SO:
   - Menu: Assets > Create > RPG > Weapon Data
   - Điền: name, type=Sword, damage=10, range=2, speed=1
5. Kéo WeaponData vào WeaponHandler.startingMainHand
6. Tạo weapon prefab (Sword mesh + Collider trigger)
   - Gắn DamageHitbox component lên weapon prefab
   - Kéo vào WeaponData.weaponPrefab
7. Gắn weapon prefab vào hand bone (hoặc để WeaponVisualHandler xử lý)
```

### Bước 4: Setup Singletons

```
1. Menu: RPG > Mega Setup Singletons
   → Tạo [RPG_Singletons] GO với:
     LootSystem, DeathSystem, DialogueSystem,
     ShopService, ZoneSystem, SaveLoadSystem,
     CraftingSystem, WeaponEnhancement, TamerSystem
```

### Bước 5: Setup Scene — Thị Trấn

```
1. Tạo scene "Town"
2. Terrain hoặc flat plane
3. Đặt Player spawn point (empty GO, tag vị trí)
4. NPC GameObjects:
   - Model NPC + Collider trigger
   - NPCInteraction script (tương lai)
   - Gắn NPCData SO
5. Portal đến Field:
   - Empty GO + Box Collider (Is Trigger)
   - Gắn Portal component
   - Set targetZone = FieldZone, targetSpawnID = "entrance"
6. Tạo ZoneData SO:
   - Menu: Assets > Create > Game > Zone Data
   - zoneID = "town", sceneName = "Town", type = Town
   - Thêm spawn points, connections
```

### Bước 6: Setup Scene — Đồng Cỏ Xanh (Field)

```
1. Tạo scene "Field_GreenMeadow"
2. Terrain với cỏ, cây, đường đi
3. SpawnZone Slime:
   - Empty GO đặt ở khu vực 1
   - Menu: RPG > Mega Setup SpawnZone
   - PackManager: baseChasers=2, enemyLevel=3
   - VAT_MobSpawner: vatEnemyPrefab=SlimePrefab, packSize=5, spawnRadius=8
   - Gắn EnemyData (Slime)
4. SpawnZone Wolf:
   - Tương tự, khu vực 2, level=5, packSize=4
5. SpawnZone Goblin:
   - Khu vực 3, level=8, packSize=5
6. Boss Zone:
   - Khu vực riêng, level=10
   - VAT_MobSpawner: packSize=1 (boss alone)
   - EnemyData: tier=Boss, HP=5000
7. Portal quay về Town:
   - Portal component, targetZone = Town
8. ZoneData SO:
   - zoneID = "field_green", sceneName = "Field_GreenMeadow"
   - type = Field, recommendedLevel = 5
   - bgmKey = "bgm_field_01"
```

### Bước 7: Tạo ScriptableObject Data

```
WEAPON DATA:
  Assets/Data/Weapons/
  ├── IronSword.asset          (Sword, 10 dmg, range 2)
  └── WoodenShield.asset       (Shield, 5 dmg, range 1)

ITEM DATA:
  Assets/Data/Items/
  ├── HPPotion_Small.asset     (Consumable, heal 50)
  ├── MPPotion_Small.asset     (Consumable, mana 30)
  ├── IronOre.asset            (Material)
  └── SlimeGel.asset           (Material, drop từ Slime)

ENEMY DATA:
  Assets/Data/Enemies/
  ├── Slime.asset              (HP:50, DMG:5, Level:3)
  ├── Wolf.asset               (HP:80, DMG:8, Level:5)
  ├── Goblin.asset             (HP:60, DMG:10, Level:8)
  └── Dragon.asset             (HP:5000, DMG:100, Level:10, Boss)

LOOT TABLE:
  Assets/Data/LootTables/
  ├── Slime_Loot.asset         (SlimeGel 50%, HPPotion 10%)
  ├── Wolf_Loot.asset          (WolfFang 40%, HPPotion 15%)
  ├── Goblin_Loot.asset        (IronOre 30%, Gold+)
  └── Dragon_Loot.asset        (IronSword guaranteed, Rare item 20%)

SKILL DATA (2 default + 2 test):
  Assets/Data/Skills/
  ├── Default_Block.asset      (block skill, mọi player có)
  ├── Default_Parry.asset      (parry skill, mọi player có)
  ├── Blade_HardHit.asset      (Kiếm Pháp tier 1, single target, 150% power)
  └── Tao_ChiBurst.asset       (Đạo Thuật tier 1, AoE circle, Chi cost)

LOCALIZATION CONFIG:
  Assets/Resources/LocalizationConfig.asset
  (defaultLanguage="vi", fallback="en")
```

### Bước 8: Setup Input

```
Project Settings > Input Manager (hoặc New Input System):

Axis:
  Horizontal → A/D
  Vertical   → W/S
  Mouse X    → mouse X
  Mouse Y    → mouse Y

Key bindings (đã config trong PlayerInputHandler):
  Attack:        Mouse Left (Mouse0)
  Heavy Attack:  Mouse Right (Mouse1)
  Block:         Q
  Lock-On:       Tab
  Sprint:        Left Shift
  Jump/Dodge:    Space
  Dash:          Left Control
  Interact:      F
  Skill 1:       1
  Skill 2:       2
  Skill 3:       3
  Skill 4:       4
  Inventory:     I  (toggle InventoryPanel)
  Skill Tree:    K  (toggle SkillTreePanel)
  Quest Log:     J  (toggle QuestPanel)
```

### Bước 9: Setup Camera

```
1. Camera GO trong scene
2. Gắn CameraController component
3. Set target = Player transform
4. Config:
   - FreeLook: distance=5, height=2, followSpeed=10
   - Combat: distance=4, height=1.5, lockOnOffset
```

### Bước 10: Validate + Test

```
1. Menu: RPG > Validate Player Setup
   → Kiểm tra 21 component, sửa nếu thiếu

2. Play mode → test:
   □ Di chuyển WASD + camera chuột
   □ Lock-on Tab → target quái
   □ Auto-attack chạy → damage numbers
   □ Skill 1-4 → thi triển skill
   □ Combo chain skill liên tiếp
   □ Dodge Space → i-frame
   □ Quái chết → EXP + Gold + Loot
   □ Level up → điểm chỉ số
   □ Inventory I → xem đồ
   □ Portal → chuyển scene
   □ Chết → DeathPanel → hồi sinh
   □ Boss fight → test full loop
```

---

## PHẦN 4: CHECKLIST TỔNG HỢP

### Asset cần chuẩn bị

```
MODEL (5 model):
  □ Player humanoid (1)
  □ Sword weapon (1)
  □ Slime enemy (1)
  □ Wolf enemy (1)
  □ Goblin enemy (1)
  □ Boss Dragon (1) — có thể để sau MVP

ANIMATION (52 clips):
  □ Player Exploration (10 clips) — Mixamo
  □ Player Combat Sword (13 clips) — Mixamo
  □ Player Combat Shared (6 clips) — Mixamo
  □ Slime animations (5 clips) — tự làm hoặc procedural
  □ Wolf animations (5 clips) — Mixamo/tự rig
  □ Goblin animations (5 clips) — Mixamo
  □ Boss animations (8 clips) — Mixamo/tự làm

SCENE (2 scene):
  □ Town — NPC, Portal, safe zone
  □ Field_GreenMeadow — 3 SpawnZone, 1 Boss zone, Portal

SCRIPTABLEOBJECT DATA (~20 asset):
  □ 2 WeaponData (Sword, Shield)
  □ 4 ItemData (HPPotion, MPPotion, IronOre, SlimeGel)
  □ 4 EnemyData (Slime, Wolf, Goblin, Dragon)
  □ 4 LootTable (mỗi enemy 1)
  □ 4 SkillData (Block, Parry, HardHit, ChiBurst)
  □ 2 ZoneData (Town, Field)
  □ 1 LocalizationConfig

AUDIO (tùy chọn, thêm sau):
  □ BGM Town
  □ BGM Field
  □ SFX: Hit, Slash, Block, Dodge, LevelUp, Death
  □ SFX: UI Click, Item Pickup
```

### Thứ tự làm

```
TUẦN 1: Model + Animation
  Ngày 1-2: Download/tạo Player model + Mixamo animations (29 clips)
  Ngày 3:   Tạo Sword model, setup Animator Controller
  Ngày 4-5: Tạo 3 enemy models + animations (15 clips)
  Ngày 6:   VAT bake tất cả enemy
  Ngày 7:   Boss model + animations (8 clips)

TUẦN 2: Scene + Data + Test
  Ngày 1:   Setup Town scene (terrain, NPC placeholder, Portal)
  Ngày 2:   Setup Field scene (terrain, SpawnZones, Boss zone)
  Ngày 3:   Tạo tất cả SO data (weapon, item, enemy, loot, skill, zone)
  Ngày 4:   Menu: RPG > Mega Setup Player → wire mọi thứ
  Ngày 5:   Test + fix: movement, combat, lock-on, dodge
  Ngày 6:   Test + fix: skill, combo, loot, level up
  Ngày 7:   Test + fix: scene transition, death, boss fight

TUẦN 3: Polish
  Ngày 1-2: UI panels (HUD đã có, thêm SkillTree/Equipment nếu cần)
  Ngày 3:   Audio (BGM + SFX cơ bản)
  Ngày 4:   VFX (hit effect, skill effect cơ bản)
  Ngày 5-7: Balance + playtest + bug fix
```

---

## PHẦN 5: CẤU TRÚC THƯ MỤC KHUYẾN NGHỊ

```
Assets/
├── Animations/
│   └── PlayerAnimator.controller
├── Data/
│   ├── Weapons/          (WeaponData SOs)
│   ├── Items/            (ItemData SOs)
│   ├── Enemies/          (EnemyData SOs)
│   ├── LootTables/       (LootTable SOs)
│   ├── Skills/           (SkillData SOs)
│   ├── SkillTrees/       (SkillTreeData SOs)
│   ├── StatusEffects/    (StatusEffectData SOs)
│   ├── Zones/            (ZoneData SOs)
│   ├── NPCs/             (NPCData SOs)
│   ├── Shops/            (ShopData SOs)
│   ├── Recipes/          (RecipeData SOs)
│   └── Pets/             (PetData SOs)
├── Models/
│   ├── Player/
│   │   ├── PlayerModel.fbx
│   │   └── Animations/   (29 FBX clips)
│   ├── Weapons/
│   │   └── Sword.fbx
│   └── Enemies/
│       ├── Slime/        (model + 5 anim FBX)
│       ├── Wolf/
│       ├── Goblin/
│       └── Boss_Dragon/
├── Prefabs/
│   ├── Player.prefab
│   ├── Weapons/
│   │   └── Sword.prefab  (mesh + DamageHitbox)
│   ├── Enemies/
│   │   ├── VAT_Slime.prefab
│   │   ├── VAT_Wolf.prefab
│   │   ├── VAT_Goblin.prefab
│   │   └── VAT_Dragon.prefab
│   └── UI/
│       └── DamagePopup.prefab
├── Resources/
│   ├── Localization/
│   │   ├── vi.json
│   │   └── en.json
│   └── LocalizationConfig.asset
├── Scenes/
│   ├── Town.unity
│   └── Field_GreenMeadow.unity
├── Shaders/              (đã có ToonLit + VAT)
├── Audio/
│   ├── BGM/
│   └── SFX/
└── Script/
    ├── RPGModular/       (82 file C# — đã hoàn chỉnh)
    └── BillGameCore_v3/  (framework)
```

---

## PHẦN 6: LƯU Ý QUAN TRỌNG

### Naming Convention — BẮT BUỘC tuân theo

```
Animation clip: {WeaponType}_{Action} hoặc Explore_{Action}
  ✅ Sword_Atk1, Explore_Idle, Dodge_Fwd
  ❌ attack1, idle_sword, dodge

Enemy VAT clip: config trong EnemyData SO
  idleClip = "Idle", walkClip = "Walk", attackClips = ["Attack1"]

ScriptableObject ID: snake_case, unique
  ✅ iron_sword, hp_potion_small, slime
  ❌ IronSword, HP Potion, Slime (spaces/PascalCase)

Localization key: dot.separated.path
  ✅ skill.blade.hard_hit.name, item.weapon.iron_sword.name
  ❌ HardHitName, iron sword name
```

### Những thứ KHÔNG cần làm cho Vertical Slice

```
❌ Tất cả 15 weapon type (chỉ cần Sword)
❌ Tất cả 14 skill tree (chỉ cần 2-3 skill test)
❌ Crafting system (code có, chưa cần UI/content)
❌ Tamer system (code có, chưa cần content)
❌ NPC dialogue phức tạp (placeholder text đủ)
❌ Save/Load test (code có, test sau)
❌ Localization đầy đủ (dùng vi.json hiện tại)
❌ Sound design hoàn chỉnh (thêm sau)
❌ UI polish (HUD cơ bản đủ)
```

---

*Kết thúc Vertical Slice Setup Guide*
*Mục tiêu: 2-3 tuần từ 0 → Playable Demo*
*Code đã sẵn sàng 100% — chỉ cần art asset + data setup*
