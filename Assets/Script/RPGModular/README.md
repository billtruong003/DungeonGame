# RPG Modular — Complete Layer 1-3 Foundation

## Quick Start

1. Copy `RPGModular/` vào `Assets/`
2. Setup Animator (xem bên dưới)
3. Attach components lên Player GameObject
4. Tạo WeaponData, kéo vào WeaponHandler
5. Play

## File Structure

```
RPGModular/
├── Interfaces/           ← Contracts (IStatProvider, IAnimationController, ICombat)
├── Core/
│   ├── Stats/            ← CharacterStats (7 stat + modifier)
│   ├── Health/           ← HealthSystem (HP/Mana/Stamina + regen)
│   ├── Animation/        ← AnimationController (code-driven, priority, phase)
│   └── Combat/
│       ├── CombatLocomotion   (lock-on movement, 75% retreat speed)
│       ├── DamagePipeline     (Crit→Dodge→Block→Defense→Min)
│       ├── LockOnSystem       (target find, switch, auto-lose)
│       ├── EnemyBase          (base template for all enemies)
│       ├── Hitbox/            (auto enable/disable per animation phase)
│       └── StateMachine/      (Idle→Engaged→Attack→Block→Parry→HitStun→Dead)
├── Weapons/              ← WeaponHandler (equip/unequip + anim sync)
├── Data/                 ← WeaponData, EnemyData (ScriptableObjects)
└── Input/                ← CombatInputHandler (attack, block, lock-on + buffer)
```

## Player Setup

Attach lên Player: CharacterStats, HealthSystem, AnimationController,
CombatStateMachine, CombatLocomotion, LockOnSystem, WeaponHandler,
CombatInputHandler, HitboxManager.

Child objects: Model (Animator), MainHand_Hitbox, OffHand_Hitbox, Body_Hitbox.

## Animator Setup

Tất cả states FLAT, không nối transition. Parameters: MoveSpeed(float),
MoveX(float), MoveY(float), IsGrounded(bool), InCombat(bool).

Convention: `{WeaponType}_{Action}` → Sword_Idle, Sword_Atk1, Bow_Idle...
