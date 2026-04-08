# RPGModular — Setup & Animator Guide

---

## FILE STRUCTURE

```
RPGModular/
├── Interfaces/
│   ├── IStatProvider.cs        (StatType, StatModifier, ModifierType)
│   ├── IAnimationController.cs (AnimationPriority, AnimationPhase, AnimationActionData)
│   ├── ICombat.cs              (DamageInfo, DamageResult, IDamageable, IDamageDealer, ITargetLockable)
│   └── IWeapon.cs              (WeaponType, WeaponSlot, WeaponAnimationSet)
├── Core/
│   ├── Stats/CharacterStats.cs
│   ├── Health/HealthSystem.cs
│   ├── Animation/AnimationController.cs
│   ├── Locomotion/
│   │   ├── LocomotionState.cs
│   │   ├── LocomotionStateMachine.cs
│   │   └── LocomotionStates.cs
│   ├── Combat/
│   │   ├── CombatLocomotion.cs
│   │   ├── DamagePipeline.cs
│   │   ├── LockOnSystem.cs
│   │   ├── EnemyBase.cs
│   │   ├── AutoAttackSystem.cs
│   │   ├── Hitbox/HitboxManager.cs
│   │   └── StateMachine/
│   │       ├── CombatState.cs
│   │       ├── CombatStateMachine.cs
│   │       └── CombatStates.cs
│   └── Player/PlayerController.cs
├── Camera/CameraController.cs
├── Weapons/WeaponHandler.cs
├── Data/WeaponData.cs
└── Input/PlayerInputHandler.cs
```

---

## PLAYER GAMEOBJECT HIERARCHY

```
Player (root)
│
├── [Components on root]
│   ├── CharacterController         (Unity built-in)
│   ├── CharacterStats
│   ├── HealthSystem
│   ├── PlayerController            (Master — bridges Exploration ↔ Combat)
│   ├── LocomotionStateMachine
│   ├── CombatStateMachine
│   ├── CombatLocomotion
│   ├── LockOnSystem
│   ├── WeaponHandler
│   ├── PlayerInputHandler
│   ├── AutoAttackSystem
│   └── HitboxManager
│
├── Model (child — 3D model with Animator)
│   ├── Animator
│   ├── AnimationController
│   ├── MainHand_Slot    (empty Transform — weapon mount point)
│   ├── OffHand_Slot     (empty Transform — shield/offhand mount)
│   └── Back_Slot        (empty Transform — weapon stowed position)
│
├── Hitboxes (child)
│   ├── MainHand_Hitbox  (BoxCollider, isTrigger=true, DamageHitbox)
│   ├── OffHand_Hitbox   (BoxCollider, isTrigger=true, DamageHitbox)
│   └── Body_Hitbox      (SphereCollider, isTrigger=true, DamageHitbox)
│
└── LockOnPoint (empty Transform at chest height)

---

MainCamera (separate GameObject)
└── CameraController
```

---

## STEP-BY-STEP SETUP

### 1. Import

Copy `RPGModular/` folder into `Assets/`.
All code lives in `namespace RPGModular`.

### 2. Layers

Create these layers in Unity (Edit → Project Settings → Tags and Layers):

| Layer | Name     | Purpose                        |
|-------|----------|--------------------------------|
| 6     | Player   | Player character               |
| 7     | Enemy    | All enemies                    |
| 8     | Ground   | Walkable terrain               |
| 9     | Hitbox   | Damage hitboxes (trigger only) |

Set collision matrix (Edit → Project Settings → Physics):
- Hitbox ↔ Player: ON
- Hitbox ↔ Enemy: ON
- Hitbox ↔ Hitbox: OFF
- Player ↔ Enemy: ON

### 3. Player Setup

1. Create empty GameObject "Player", set layer to Player
2. Add `CharacterController` (Height=2, Radius=0.3, Center=(0,1,0))
3. Add these components (order doesn't matter, auto-wiring handles refs):
   - `CharacterStats`
   - `HealthSystem`
   - `PlayerController`
   - `LocomotionStateMachine`
   - `CombatStateMachine`
   - `CombatLocomotion`
   - `LockOnSystem`
   - `WeaponHandler`
   - `PlayerInputHandler`
   - `AutoAttackSystem`
   - `HitboxManager`

4. Drag your 3D character model as child, name it "Model"
5. Add `AnimationController` to the Model (same GO as Animator)
6. Create Hitbox children (see Hitbox Setup below)
7. Create empty child "LockOnPoint" at chest height (y≈1.2)

### 4. Inspector Wiring

Most fields auto-populate via `GetComponent` / `GetComponentInChildren`.
Manually wire these for safety:

**On PlayerController:**
- Drag `LocomotionStateMachine` → locomotion
- Drag `CombatStateMachine` → combat
- Drag `CameraController` (from MainCamera) → cameraController
- Set `enemyLayer` = Enemy layer

**On LocomotionStateMachine:**
- Set `groundLayer` = Ground layer

**On LockOnSystem:**
- Set `targetLayer` = Enemy layer
- Set `searchRadius` = 15
- Set `maxLockDistance` = 20

**On HitboxManager:**
- Drag the 3 DamageHitbox children → mainHandHitbox, offHandHitbox, bodyHitbox

**On WeaponHandler:**
- Drag your WeaponData ScriptableObject → startingMainHand

### 5. Camera Setup

1. Select MainCamera
2. Add `CameraController`
3. Drag Player root → target

### 6. Hitbox Setup

Create 3 child GameObjects under Player:

**MainHand_Hitbox:**
- Add `BoxCollider` (isTrigger = true)
- Add `DamageHitbox`, set AttachedSlot = MainHand
- Size collider to match weapon swing area
- Parent this to the hand bone or weapon mount for it to follow the weapon

**OffHand_Hitbox:**
- Same setup, AttachedSlot = OffHand
- For shield bash or offhand weapon

**Body_Hitbox:**
- `SphereCollider` (isTrigger = true)
- `DamageHitbox`, AttachedSlot = MainHand
- For unarmed attacks (punch, kick range)

### 7. Create WeaponData

Right-click in Project → Create → RPG → Weapon Data

Example "Iron Sword":
- weaponName = "Iron Sword"
- type = Sword
- slot = MainHand
- primaryDamageType = Slash
- damageGroup = Slash
- baseDamage = 15
- attackRange = 2.5
- attackSpeedModifier = 1.0
- useDefaultAnimSet = true

Drag into WeaponHandler → startingMainHand

### 8. Enemy Setup

```
Enemy (root, layer = Enemy)
├── [Components]
│   ├── EnemyBase (or subclass)
│   ├── Collider (CapsuleCollider, non-trigger — for physics)
│   └── Rigidbody (isKinematic = true)
├── Model (child)
│   ├── Animator
│   └── AnimationController
├── LockOnPoint (empty Transform at chest)
└── Hitbox (BoxCollider isTrigger, DamageHitbox)
```

Create EnemyData: Right-click → Create → RPG → Enemy Data.

---

## ANIMATOR SETUP

### Core Principle

ALL animation states are FLAT in the Animator — NO transition arrows between them.
Every transition is driven by `AnimationController.CrossFade()` from code.

### Required Parameters

Create these parameters in the Animator Controller:

| Parameter  | Type  | Usage                             |
|------------|-------|-----------------------------------|
| MoveSpeed  | Float | Locomotion blend (0=idle, 1=max)  |
| MoveX      | Float | Combat strafe direction (-1 to 1) |
| MoveY      | Float | Combat fwd/back direction (-1 to 1)|
| IsGrounded | Bool  | Ground state for jump/fall        |
| InCombat   | Bool  | Combat mode toggle                |

### Exploration Animations (SHARED — not per-weapon)

These states are used when OUT of combat. They are the same regardless of weapon.

| State Name  | Type       | Description                         |
|-------------|------------|-------------------------------------|
| Idle        | Single     | Relaxed standing, weapon stowed     |
| Walk        | Single     | Walking forward                     |
| Run         | Single     | Running forward                     |
| Jump        | Single     | Jump launch upward                  |
| DoubleJump  | Single     | Second jump mid-air (flip/boost)    |
| Fall        | Single     | Falling/airborne                    |
| Land        | Single     | Soft landing recovery               |
| HardLand    | Single     | Hard landing (long fall) crouch     |
| Dash        | Single     | Quick forward dash/burst            |

**REUSABLE:** All exploration animations are weapon-agnostic.
One set works for every build.
Use a Blend Tree for Idle → Walk → Run driven by `MoveSpeed`.

### Blend Tree: Locomotion

Create a 1D Blend Tree called "Locomotion_BlendTree":
- Parameter: MoveSpeed
- Thresholds: 0.0 = Idle, 0.4 = Walk, 1.0 = Run
- This replaces individual Idle/Walk/Run states
- Set as the default state in the Animator

### Combat Animations (PER-WEAPON)

Each weapon type needs its own set. Naming convention: `{WeaponType}_{Action}`

#### States Required Per Weapon Type

| State Name Pattern      | Example (Sword)      | Description                |
|-------------------------|----------------------|----------------------------|
| {Type}_Idle             | Sword_Idle           | Combat stance              |
| {Type}_Walk_Fwd         | Sword_Walk_Fwd       | Walk toward target         |
| {Type}_Walk_Back        | Sword_Walk_Back      | Walk away from target      |
| {Type}_Walk_Left        | Sword_Walk_Left      | Strafe left                |
| {Type}_Walk_Right       | Sword_Walk_Right     | Strafe right               |
| {Type}_Atk1             | Sword_Atk1           | Combo hit 1                |
| {Type}_Atk2             | Sword_Atk2           | Combo hit 2                |
| {Type}_Atk3             | Sword_Atk3           | Combo hit 3                |
| {Type}_Atk4 (if needed) | Dagger_Atk4          | Combo hit 4 (Dagger, etc.) |
| {Type}_Block            | Sword_Block          | Block hold pose            |
| {Type}_Block_Hit        | Sword_Block_Hit      | Block impact reaction      |
| {Type}_Block_Break      | Sword_Block_Break    | Guard broken stagger       |
| {Type}_Hit_Light        | Sword_Hit_Light      | Light hit flinch           |
| {Type}_Hit_Heavy        | Sword_Hit_Heavy      | Heavy hit stagger          |
| {Type}_Knockback        | Sword_Knockback      | Blown back                 |
| {Type}_Equip            | Sword_Equip          | Draw weapon                |
| {Type}_Unequip          | Sword_Unequip        | Sheathe weapon             |

#### Combo Chain Length Per Weapon

| WeaponType  | Combo Hits | Speed    |
|-------------|------------|----------|
| Unarmed     | 3          | Fast     |
| Sword       | 3          | Medium   |
| GreatSword  | 3          | Slow     |
| Shield      | 2          | Slow     |
| Spear       | 3          | Medium   |
| Halberd     | 3          | Med-Slow |
| Bow         | 3          | Fast     |
| Bowgun      | 3          | Fast     |
| Staff       | 2          | V.Slow   |
| MagicDevice | 2          | Medium   |
| Dagger      | 4          | V.Fast   |
| Knuckle     | 4          | V.Fast   |
| Katana      | 3          | Medium   |
| DualWield   | 4          | Fast     |
| Axe         | 3          | Med-Slow |

### Shared / Universal Animations

These states are used across all weapon types (create once):

| State Name     | Used By         | Description                        |
|----------------|-----------------|------------------------------------|
| Death          | Everyone        | Death fall                         |
| Parry_Success  | Player          | Parry flash pose                   |
| GuardBreak     | Player          | Stamina-depleted stagger           |
| Dodge_Fwd      | Player          | Forward dodge roll/step            |
| Dodge_Back     | Player          | Backward dodge roll/step           |
| Dodge_Left     | Player          | Left dodge roll/step               |
| Dodge_Right    | Player          | Right dodge roll/step              |
| Revive         | Player          | Stand up from death                |

**REUSABLE:** Dodge animations are weapon-agnostic. One set of 4 dodge anims works for all weapons.

### Enemy-Specific Animations

| State Name       | Description              |
|------------------|--------------------------|
| Enemy_Atk1       | Normal attack            |
| Enemy_Atk_Heavy  | Heavy/charged attack     |
| Enemy_Hit_Light  | Flinch                   |
| Enemy_Hit_Heavy  | Heavy stagger            |
| Enemy_Dodge      | Enemy dodge/sidestep     |
| Enemy_Block_Hit  | Block reaction           |
| Enemy_Death      | Death                    |

---

## ANIMATION REUSE GUIDE

### Which Animations Are Shared (Buy/Create Once)

These work across ALL weapon types — highest reuse value:

1. **Exploration Set** (Idle, Walk, Run, Jump, Fall, Land, Dash, DoubleJump, HardLand)
   → 9 animations, used 100% of the time outside combat

2. **Dodge Set** (Dodge_Fwd, Dodge_Back, Dodge_Left, Dodge_Right)
   → 4 animations, shared across all weapons

3. **Universal Combat** (Death, Parry_Success, GuardBreak, Revive)
   → 4 animations, shared

4. **Hit Reactions** — CAN be shared if weapon doesn't affect the reaction:
   → Generic_Hit_Light, Generic_Hit_Heavy, Generic_Knockback
   → Set these as fallback in custom WeaponAnimationSet

**Total shared animations: ~17**

### Which Animations Are Per-Weapon

Each weapon type needs:
- 1 Combat Idle
- 4 Combat Walk (Fwd/Back/Left/Right) — OR use a 2D Blend Tree
- 2-4 Attack animations (combo chain)
- 3 Block animations (Idle, Hit, Break)
- 2 Equip/Unequip
- 3 Hit reactions (can reuse generic if acceptable)

**Per weapon: 15-19 unique animations**
**With generic hit reactions: 12-16 unique animations**

### Using Blend Trees for Combat Locomotion

Instead of 4 separate Walk states per weapon, use a 2D Blend Tree:

1. Create a 2D Freeform Directional blend tree named `{Type}_CombatMove`
2. Parameter X = MoveX, Parameter Y = MoveY
3. Map:
   - (0, 0) → {Type}_Idle
   - (0, 1) → {Type}_Walk_Fwd
   - (0, -1) → {Type}_Walk_Back
   - (-1, 0) → {Type}_Walk_Left
   - (1, 0) → {Type}_Walk_Right

This reduces 5 states to 1 blend tree per weapon.

### Mixamo / Asset Store Animation Mapping

When using Mixamo or store-bought animations:

1. Import as Humanoid rig (Avatar Definition = Create From This Model)
2. Rename state in Animator to match convention: `{WeaponType}_{Action}`
3. For animations not designed for your weapon:
   - Use Avatar Masks to split upper/lower body
   - Lower body: shared walk cycle
   - Upper body: weapon-specific swing

### Priority When Buying Animation Packs

Invest budget in this order:
1. **Exploration locomotion** (idle/walk/run/jump) — used most
2. **Sword set** — most common weapon, highest play-time
3. **Dodge set** — shared, high visibility
4. **Additional weapons** — as needed per game content

---

## COMBAT FLOW DIAGRAM

```
[Exploration Mode]
    │
    ├── Player attacks enemy / Enemy aggros → Lock-On triggers
    │
    ▼
[Equip Animation] (weapon draw, 0.6s)
    │
    ▼
[Combat Mode — CombatEngagedState]
    │
    ├── Auto-Attack when in range (Toram-style)
    ├── Manual Attack (Mouse Left) → AttackingState
    │   ├── Startup phase (can cancel with Dodge)
    │   ├── Active phase (hitbox ON)
    │   └── Recovery phase (accept combo input)
    │
    ├── Heavy Attack (Mouse Right) → AttackingState (heavy)
    │
    ├── Block (hold Q) → BlockingState
    │   ├── First frames = Parry window (TECH stat)
    │   ├── Parry success → ParrySuccessState → Riposte window
    │   ├── Stamina depleted → GuardBreakState
    │   └── Release Q → return to Engaged
    │
    ├── Dodge (Space / double-tap direction) → DodgeState
    │   ├── I-frames during first 0.25s
    │   └── Direction based on input
    │
    ├── Hit → HitStunState
    │   ├── Light hit: flinch 0.3s
    │   └── Heavy hit: knockback 0.6s
    │
    └── Death → DeadState
    
[No enemies nearby for 5s] → Unequip Animation → [Exploration Mode]
```

---

## MODE TRANSITION (PlayerController)

```
Exploration ←→ Combat switching:

Exploration → Combat triggers:
  1. Player presses Attack near enemy → auto lock-on → equip weapon → combat
  2. Player presses Tab on enemy → lock-on → equip weapon → combat
  3. Enemy aggros and starts attacking → auto lock-on → equip weapon → combat
  4. Boss zone enter → forced combat

Combat → Exploration triggers:
  1. No enemies in aggro range for 5 seconds → unequip weapon → exploration
  2. Forced by script (cutscene, safe zone)

During transition:
  - LocomotionStateMachine is disabled in combat
  - CombatStateMachine is disabled in exploration
  - Equip/Unequip animation plays during 0.5-0.6s transition
  - Camera smoothly switches between FreeLook and Combat mode
```

---

## KEY BINDINGS (Default)

| Key              | Exploration        | Combat              |
|------------------|--------------------|----------------------|
| WASD             | Move               | Strafe/Approach      |
| Mouse Right Hold | Rotate camera      | (auto-track target)  |
| Left Shift       | Sprint             | —                    |
| Space            | Jump               | Dodge                |
| Left Ctrl        | Dash               | —                    |
| Mouse Left       | Attack (enter combat)| Normal Attack       |
| Mouse Right      | —                  | Heavy Attack         |
| Q                | —                  | Block (hold)         |
| Tab              | Lock-On toggle     | Lock-On toggle       |
| [ ]              | —                  | Switch target L/R    |
| F                | Interact           | —                    |

---

## STAT FORMULAS REFERENCE

| Derived Stat    | Formula                                             | Clamp       |
|-----------------|-----------------------------------------------------|-------------|
| Max HP          | 100 + VIT × 15                                      | —           |
| Max Mana        | 50 + INT × 12                                       | —           |
| Max Stamina     | 100 + VIT × 8 + AGI × 4                             | —           |
| Physical Attack | STR × 2 + DEX × 0.5                                 | —           |
| Magic Attack    | INT × 2.5                                           | —           |
| Physical Def    | VIT × 1.5 + STR × 0.3                               | —           |
| Magic Def       | INT × 1.2 + VIT × 0.5                               | —           |
| Attack Speed    | 1.0 + (AGI-10)×0.02 + (DEX-10)×0.01                 | 0.5 – 2.0   |
| Move Speed      | 5.0 + (AGI-10)×0.15                                 | 3.0 – 10.0  |
| Crit Chance     | 5% + DEX×0.5% + LUK×0.3%                            | 0% – 75%    |
| Crit Damage     | 150% + LUK×1.5%                                     | 150%+       |
| Dodge Chance    | AGI×0.4% + LUK×0.2%                                 | 0% – 50%    |
| Parry Window    | 0.15s + TECH×0.005s                                  | 0.1s – 0.5s |

---

## DAMAGE PIPELINE

```
Raw Damage (ATK + weapon base)
    │
    ▼
[1] Crit Check (DEX + LUK → chance, LUK → multiplier)
    │
    ▼
[2] Dodge Check (AGI + LUK → chance, if success → damage = 0)
    │
    ▼
[3] Block Check (if defender blocking → reduce 70%, heavy → reduce 40% + knockback)
    │
    ▼
[4] Defense Reduction: damage × (100 / (100 + defense))
    │
    ▼
[5] Minimum Damage = max(result, 1)
    │
    ▼
Final Damage applied to HP
```

---

## QUICK START CHECKLIST

- [ ] Import RPGModular into Assets
- [ ] Create Player layer, Enemy layer, Ground layer
- [ ] Set up physics collision matrix
- [ ] Create Player GO with all components (see Player Setup)
- [ ] Create Model child with Animator + AnimationController
- [ ] Set up Animator Controller with parameters (MoveSpeed, MoveX, MoveY, IsGrounded, InCombat)
- [ ] Add Locomotion blend tree (Idle → Walk → Run on MoveSpeed)
- [ ] Add all flat animation states (no transitions between them)
- [ ] Create 3 Hitbox children with DamageHitbox + trigger Colliders
- [ ] Wire HitboxManager references in Inspector
- [ ] Create WeaponData ScriptableObject, assign to WeaponHandler
- [ ] Set up CameraController on MainCamera, wire target
- [ ] Set LockOnSystem targetLayer to Enemy
- [ ] Set PlayerController enemyLayer to Enemy
- [ ] Set LocomotionStateMachine groundLayer to Ground
- [ ] Create Enemy GO with EnemyBase + EnemyData + AnimationController
- [ ] Set enemy layer to Enemy
- [ ] Press Play
